using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;

namespace Modinstaller2.Models
{
    public class ModItem : ReactiveObject
    {
        private static readonly string[] BLACKLIST =
        {
            "hollow_Knight_Data/",
            "Managed/",
            "Mods/"
        };

        private static readonly SemaphoreSlim _InstallSem = new (1);

        public ModItem(ModState state, string[] dependencies, string[] files, string link, string name, string description, Settings config)
        {
            _state = state;
            Dependencies = dependencies;
            Files = files;
            Link = link;
            Name = name;
            Description = description;
            Config = config;
        }
        
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

        public bool EnabledIsChecked => State switch
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
        public bool? InstalledIsChecked => State switch
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

        public void OnEnable()
        {
            if (State is not InstalledState state)
                throw new InvalidOperationException("Cannot enable mod which is not installed!");

            if (!Directory.Exists(Config.DisabledFolder))
                Directory.CreateDirectory(Config.DisabledFolder);

            foreach (string file in Files)
            {
                string disabledPath = Path.Combine(Config.DisabledFolder, file);
                string enabledPath = Path.Combine(Config.ModsFolder, file);

                if (state.Enabled)
                {
                    if (File.Exists(disabledPath))
                        File.Delete(disabledPath);

                    File.Move(enabledPath, disabledPath);
                }
                else
                {
                    if (File.Exists(enabledPath))
                        throw new Exception("File already exists!");

                    File.Move(disabledPath, enabledPath);
                }
            }

            State = state with { Enabled = !state.Enabled };
        }

        public async Task OnInstall(IList<ModItem> items, Action<bool> setProgressBar, Action<double> setProgress)
        {
            if (State is InstalledState state)
            {
                // If we're not updated, update
                if (!state.Updated)
                {
                    setProgressBar(true);

                    await _InstallSem.WaitAsync();

                    try
                    {
                        await Install(items, setProgress, state.Enabled);
                    }
                    finally
                    {
                        _InstallSem.Release();
                    }

                    setProgressBar(false);

                    State = state with { Updated = true };
                }
                // Otherwise the user wanted to uninstall.
                else
                {
                    Uninstall(items);

                    State = new NotInstalledState();
                }
            }
            else
            {
                State = (NotInstalledState) State with { Installing = true };

                setProgressBar(true);

                await _InstallSem.WaitAsync();

                try
                {
                    await Install(items, setProgress, true);
                }
                finally
                {
                    _InstallSem.Release();
                }

                setProgressBar(false);

                State = new InstalledState(Updated: true, Enabled: true);
            }
        }

        private async Task Install(IList<ModItem> items, Action<double> setProgress, bool enable)
        {
            foreach (ModItem dep in Dependencies.Select(x => items.FirstOrDefault(i => i.Name == x)).Where(x => x != null))
            {
                if (dep.State is InstalledState { Updated: true })
                    continue;

                ModState prev_state = dep.State;

                // Enable the dependencies' dependencies if we're enabling this mod
                // Or if the dependency was previously not installed.
                await dep.Install(items, _ => { }, enable || dep.State is NotInstalledState);

                // If we were disabled before, keep the enabled state of the dependency
                if (!enable && prev_state is InstalledState installed)
                    dep.State = installed with { Updated = true };
                else
                    dep.State = new InstalledState(true, true);
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

            if (!string.IsNullOrEmpty(dl.ResponseHeaders["Content-Disposition"]))
            {
                var disposition = new ContentDisposition(dl.ResponseHeaders["Content-Disposition"]);

                filename = disposition.FileName;
            }

            if (string.IsNullOrEmpty(filename))
            {
                filename = Link[(Link.LastIndexOf("/") + 1)..];
            }

            string ext = Path.GetExtension(filename.ToLower());

            // Default to enabling
            string mod_folder = enable
                ? Config.ModsFolder
                : Config.DisabledFolder;

            if (!Directory.Exists(mod_folder))
                Directory.CreateDirectory(mod_folder);

            switch (ext)
            {
                case ".zip":
                {
                    using var archive = new ZipArchive(new MemoryStream(data));

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("dll"))
                        {
                            entry.ExtractToFile
                            (
                                entry.Name.StartsWith
                                    ("Assembly-CSharp")
                                    ? Path.Combine(Config.ManagedFolder, entry.Name)
                                    : Path.Combine(mod_folder, entry.Name),
                                true
                            );
                        }
                        else if (entry.Name.StartsWith("README"))
                        {
                            // TODO: Handle README
                        }
                        // Folder, ignore if it's one of the base direcetories.
                        else if (string.IsNullOrEmpty(entry.Name) && BLACKLIST.All(i => !entry.FullName.EndsWith(i, StringComparison.OrdinalIgnoreCase)))
                        {
                            // If we're using the full zip structure, remove the prev directories.
                            string dirPath = entry.FullName.Replace("hollow_knight_Data/Managed/Mods/", string.Empty);

                            Directory.CreateDirectory(Path.Combine(Config.ModsFolder, dirPath));
                        }
                        // Any other file, we place it according to the normal structure
                        else if (entry.Name != string.Empty)
                        {
                            // Once again, ignore the first few folders of zip structure.
                            string path = entry.FullName.Replace("hollow_knight_Data/Managed/Mods/", string.Empty);

                            // Something higher up than mods, annoying.
                            if (path.StartsWith("hollow_knight_Data"))
                            {
                                path = entry.FullName.Replace("hollow_knight_Data/Managed/", string.Empty);

                                try
                                {
                                    entry.ExtractToFile(Path.Combine(Config.ManagedFolder, path), true);
                                }
                                catch (DirectoryNotFoundException)
                                {
                                    Debug.WriteLine($"Unable to find directory in path {path}");
                                }
                            }
                            else
                            {
                                if (!Directory.Exists(Path.GetDirectoryName(path)) && Path.GetFileName(path).EndsWith(".dll"))
                                {
                                    Debug.WriteLine($"[WARN] Directory sub-path does not exist, extracting to Managed. {path}");

                                    entry.ExtractToFile(Path.Combine(Config.ModsFolder, Path.GetFileName(path)), true);
                                }
                                else
                                {
                                    entry.ExtractToFile(Path.Combine(Config.ModsFolder, path), true);
                                }
                            }
                        }
                    }

                    break;
                }

                case ".dll":
                {
                    await File.WriteAllBytesAsync(Path.Combine(mod_folder, filename), data);

                    break;
                }

                default:
                {
                    throw new NotImplementedException($"Unknown file type for mod download: {filename}");
                }
            }
        }

        private void Uninstall(IList<ModItem> items)
        {
            foreach (string file in Files)
            {
                string path = Path.Combine
                (
                    State is InstalledState { Enabled: true }
                        ? Config.ModsFolder
                        : Config.DisabledFolder,
                    file
                );

                if (File.Exists(path))
                    File.Delete(path);
            }

            foreach (ModItem dep in Dependencies.Select(x => items.FirstOrDefault(i => x == i.Name)).Where(x => x != null))
            {
                // Make sure no other mods depend on it
                if (items.Where(x => x.State is InstalledState && x != this).Any(x => x.Dependencies.Contains(dep.Name)))
                    continue;

                dep.Uninstall(items);

                dep.State = new NotInstalledState();
            }
        }
    }
}