using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Modinstaller2.Services;
using ReactiveUI;

namespace Modinstaller2.Models
{
    public class ModItem : ReactiveObject
    {
        private static readonly SemaphoreSlim _InstallSem = new(1);

        public ModItem
        (
            ModState state,
            Version version,
            string[] dependencies,
            string[] files,
            string link,
            string name,
            string description,
            Settings config
        )
        {
            _state = state;

            Version = version;
            Dependencies = dependencies;
            Files = files;
            Link = link;
            Name = name;
            Description = description;
            Config = config;
        }


        public Version  Version      { get; }
        public string[] Dependencies { get; }
        public string[] Files        { get; }
        public string   Link         { get; }
        public string   Name         { get; }
        public string   Description  { get; }
        public Settings Config       { get; }

        private ModState _state;

        public ModState State
        {
            get => _state;
            set
            {
                _state = value;

                this.RaisePropertyChanged(nameof(State));
                this.RaisePropertyChanged(nameof(InstalledIsChecked));
                this.RaisePropertyChanged(nameof(EnabledIsChecked));
                this.RaisePropertyChanged(nameof(Color));
                this.RaisePropertyChanged(nameof(Installed));
            }
        }

        public bool EnabledIsChecked =>
            State switch
            {
                InstalledState { Enabled: var x } => x,

                // Can't enable what isn't installed.
                _ => false
            };

        // 
        // Update required -> null
        // Installed -> true
        // Not installed -> false
        // Installing -> true, but different color.
        //
        // We use null for updates so we get 
        // a box in the UI, which is a nice indicator.
        public bool? InstalledIsChecked =>
            State switch
            {
                InstalledState { Updated: true } => true,
                InstalledState { Updated: false } => null,
                NotInstalledState { Installing: true } => true,
                _ => false
            };

        public bool Installing => State is NotInstalledState m && m.Installing;

        public Color Color => Color.Parse(State is InstalledState { Updated : true } ? "#ff086f9e" : "#f49107");

        public string InstallText => State is InstalledState { Updated: false } ? "Out of date!" : "Installed?";

        public bool Installed => State is InstalledState;

        private static void CreateNeededDirectories(Settings config)
        {
            if (!Directory.Exists(config.DisabledFolder))
                Directory.CreateDirectory(config.DisabledFolder);

            if (!Directory.Exists(config.ModsFolder))
                Directory.CreateDirectory(config.ModsFolder);
        }

        public void OnEnable()
        {
            if (State is not InstalledState state)
                throw new InvalidOperationException("Cannot enable mod which is not installed!");

            CreateNeededDirectories(Config);

            var (prev, after) = !state.Enabled
                ? (Config.DisabledFolder, Config.ModsFolder) 
                : (Config.ModsFolder, Config.DisabledFolder);
            
            Directory.Move(Path.Combine(prev, Name), Path.Combine(after, Name));
            
            State = state with { Enabled = !state.Enabled };
        }

        public async Task OnInstall(IServiceProvider sp, Action<bool> setProgressBar, Action<double> setProgress)
        {
            IList<ModItem> items = sp.GetRequiredService<ModDatabase>().Items;

            var mods = sp.GetRequiredService<InstalledMods>();

            CreateNeededDirectories(Config);

            if (State is InstalledState(var enabled, var updated))
            {
                // If we're not updated, update
                if (!updated)
                {
                    setProgressBar(true);

                    await _InstallSem.WaitAsync();

                    try
                    {
                        await Install(mods, items, setProgress, enabled);
                    }
                    finally
                    {
                        _InstallSem.Release();
                    }

                    setProgressBar(false);
                }
                // Otherwise the user wanted to uninstall.
                else
                {
                    await Uninstall(mods, items);
                }
            }
            else
            {
                State = (NotInstalledState) State with { Installing = true };

                setProgressBar(true);

                await _InstallSem.WaitAsync();

                try
                {
                    await Install(mods, items, setProgress, true);
                }
                finally
                {
                    _InstallSem.Release();
                }

                setProgressBar(false);
            }
        }

        private async Task Install(InstalledMods mods, IList<ModItem> items, Action<double> setProgress, bool enable)
        {
            foreach (ModItem dep in Dependencies.Select(x => items.First(i => i.Name == x)))
            {
                if (dep.State is InstalledState { Updated: true })
                    continue;

                // Enable the dependencies' dependencies if we're enabling this mod
                // Or if the dependency was previously not installed.
                await dep.Install(mods, items, _ => { }, enable || dep.State is NotInstalledState);
            }

            var dl = new WebClient();

            setProgress(0);

            dl.DownloadProgressChanged += (_, args) =>
            {
                if (args.TotalBytesToReceive < 0)
                {
                    setProgress(-1);

                    return;
                }

                setProgress(100 * args.BytesReceived / (double) args.TotalBytesToReceive);
            };

            byte[] data = await dl.DownloadDataTaskAsync(new Uri(Link));

            string filename = string.Empty;

            if (!string.IsNullOrEmpty(dl.ResponseHeaders?["Content-Disposition"]))
            {
                var disposition = new ContentDisposition(dl.ResponseHeaders["Content-Disposition"] ?? throw new InvalidOperationException());

                filename = disposition.FileName ?? throw new InvalidOperationException();
            }

            if (string.IsNullOrEmpty(filename))
            {
                filename = Link[(Link.LastIndexOf("/") + 1)..];
            }

            string ext = Path.GetExtension(filename.ToLower());
            string fbase = Path.GetFileNameWithoutExtension(filename);

            // Default to enabling
            string base_folder = enable
                ? Config.ModsFolder
                : Config.DisabledFolder;

            string mod_folder = Path.Combine(base_folder, fbase);

            switch (ext)
            {
                case ".zip":
                {
                    ExtractZipToPath(data, mod_folder);

                    break;
                }

                case ".dll":
                {
                    Directory.CreateDirectory(mod_folder);

                    await File.WriteAllBytesAsync(Path.Combine(mod_folder, filename), data);

                    break;
                }

                default:
                {
                    throw new NotImplementedException($"Unknown file type for mod download: {filename}");
                }
            }

            State = _state switch
            {
                InstalledState installed => installed with
                {
                    Updated = true,
                    Enabled = enable
                },

                NotInstalledState => new InstalledState(enable, true),

                _ => throw new InvalidOperationException(_state.GetType().Name)
            };

            await mods.RecordInstall(this);
        }

        private async Task Uninstall(InstalledMods mods, IList<ModItem> items)
        {
            string dir = Path.Combine
            (
                State is InstalledState { Enabled: true }
                    ? Config.ModsFolder
                    : Config.DisabledFolder,
                Name
            );

            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            State = new NotInstalledState();

            await mods.RecordUninstall(this);

            foreach (ModItem dep in Dependencies.Select(x => items.First(i => x == i.Name)))
            {
                // Make sure no other mods depend on it
                if (items.Where(x => x.State is InstalledState && x != this).Any(x => x.Dependencies.Contains(dep.Name)))
                    continue;

                await dep.Uninstall(mods, items);

                dep.State = new NotInstalledState();
            }
        }

        private static void ExtractZipToPath(byte[] raw_zip_data, string root_folder)
        {
            using var archive = new ZipArchive(new MemoryStream(raw_zip_data));
            
            string dest_dir_path = CreateDirectoryPath(root_folder);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string file_dest = Path.GetFullPath(Path.Combine(dest_dir_path, entry.FullName));

                if (!file_dest.StartsWith(dest_dir_path))
                    throw new IOException("Extracts outside of directory!");

                // If it's a directory:
                if (Path.GetFileName(file_dest).Length == 0)
                {
                    Directory.CreateDirectory(file_dest);
                }
                // File
                else
                {
                    // Create containing directory:
                    Directory.CreateDirectory(Path.GetDirectoryName(file_dest)!);

                    entry.ExtractToFile(file_dest, true);
                }
            }
        }

        private static string CreateDirectoryPath(string path)
        {
            // Note that this will give us a good DirectoryInfo even if destinationDirectoryName exists:
            DirectoryInfo di = Directory.CreateDirectory(path);

            string dest_dir_path = di.FullName;

            if (!dest_dir_path.EndsWith(Path.DirectorySeparatorChar))
                dest_dir_path += Path.DirectorySeparatorChar;

            return dest_dir_path;
        }
    }
}