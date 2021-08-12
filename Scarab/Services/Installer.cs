using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Scarab.Interfaces;
using Scarab.Models;

namespace Scarab.Services
{
    public class Installer : IInstaller
    {
        private readonly ISettings _config;
        private readonly IModSource _installed;
        private readonly IModDatabase _db;

        private readonly SemaphoreSlim _semaphore = new(1);

        public Installer(ISettings config, IModSource installed, IModDatabase db)
        {
            _config = config;
            _installed = installed;
            _db = db;
        }
        
        private void CreateNeededDirectories()
        {
            // These both no-op if the directory already exists,
            // so no need to check ourselves
            Directory.CreateDirectory(_config.DisabledFolder);

            Directory.CreateDirectory(_config.ModsFolder);
        }

        public void Toggle(ModItem mod)
        {
            if (mod.State is not InstalledState state)
                throw new InvalidOperationException("Cannot enable mod which is not installed!");

            CreateNeededDirectories();

            var (prev, after) = !state.Enabled
                ? (_config.DisabledFolder, _config.ModsFolder)
                : (_config.ModsFolder, _config.DisabledFolder);

            Directory.Move(Path.Combine(prev, mod.Name), Path.Combine(after, mod.Name));

            mod.State = state with { Enabled = !state.Enabled };
        }


        public async Task Install(ModItem mod, Action<double> setProgress, bool enable)
        {
            await _semaphore.WaitAsync();

            try
            {
                CreateNeededDirectories();
                
                await _Install(mod, setProgress, enable);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task Uninstall(ModItem mod)
        {
            await _semaphore.WaitAsync();

            try
            {
                // Shouldn't ever not exist, but rather safe than sorry I guess.
                CreateNeededDirectories();
                
                await _Uninstall(mod);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task _Install(ModItem mod, Action<double> setProgress, bool enable)
        {
            foreach (ModItem dep in mod.Dependencies.Select(x => _db.Items.First(i => i.Name == x)))
            {
                if (dep.State is InstalledState { Updated: true })
                    continue;

                // Enable the dependencies' dependencies if we're enabling this mod
                // Or if the dependency was previously not installed.
                await _Install(dep, _ => { }, enable || dep.State is NotInstalledState);
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

                setProgress(100 * args.BytesReceived / (double)args.TotalBytesToReceive);
            };

            byte[] data = await dl.DownloadDataTaskAsync(new Uri(mod.Link));

            string filename = string.Empty;

            if (!string.IsNullOrEmpty(dl.ResponseHeaders?["Content-Disposition"]))
            {
                var disposition = new ContentDisposition(dl.ResponseHeaders["Content-Disposition"] ?? throw new InvalidOperationException());

                filename = disposition.FileName ?? throw new InvalidOperationException();
            }

            if (string.IsNullOrEmpty(filename))
            {
                filename = mod.Link[(mod.Link.LastIndexOf("/") + 1)..];
            }

            string ext = Path.GetExtension(filename.ToLower());
            string fbase = Path.GetFileNameWithoutExtension(filename);

            // Default to enabling
            string base_folder = enable
                ? _config.ModsFolder
                : _config.DisabledFolder;

            string mod_folder = Path.Combine(base_folder, fbase);

            switch (ext)
            {
                case ".zip":
                {
                    using var archive = new ZipArchive(new MemoryStream(data));

                    // Note that this will give us a good DirectoryInfo even if destinationDirectoryName exists:
                    DirectoryInfo di = Directory.CreateDirectory(mod_folder);

                    string dest_dir_path = di.FullName;

                    if (!dest_dir_path.EndsWith(Path.DirectorySeparatorChar))
                        dest_dir_path += Path.DirectorySeparatorChar;

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

            mod.State = mod.State switch
            {
                InstalledState installed => installed with
                {
                    Version = mod.Version,
                    Updated = true,
                    Enabled = enable
                },

                NotInstalledState => new InstalledState(enable, mod.Version, true),

                _ => throw new InvalidOperationException(mod.State.GetType().Name)
            };

            await _installed.RecordInstall(mod);
        }

        private async Task _Uninstall(ModItem mod)
        {
            string dir = Path.Combine
            (
                mod.State is InstalledState { Enabled: true }
                    ? _config.ModsFolder
                    : _config.DisabledFolder,
                mod.Name
            );

            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            mod.State = new NotInstalledState();

            await _installed.RecordUninstall(mod);

            foreach (ModItem dep in mod.Dependencies.Select(x => _db.Items.First(i => x == i.Name)))
            {
                // Make sure no other mods depend on it
                if (_db.Items.Where(x => x.State is InstalledState && x != mod).Any(x => x.Dependencies.Contains(dep.Name)))
                    continue;

                await _Uninstall(dep);

                dep.State = new NotInstalledState();
            }
        }
    }
}