using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Abstractions;
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
        private enum Update
        {
            ForceUpdate,
            LeaveUnchanged
        }

        private readonly ISettings _config;
        private readonly IModSource _installed;
        private readonly IModDatabase _db;
        private readonly IFileSystem _fs;

        private const string Modded = "Assembly-CSharp.dll.m";
        private const string Vanilla = "Assembly-CSharp.dll.v";
        private const string Current = "Assembly-CSharp.dll";

        private readonly SemaphoreSlim _semaphore = new (1);

        public Installer(ISettings config, IModSource installed, IModDatabase db, IFileSystem fs)
        {
            _config = config;
            _installed = installed;
            _db = db;
            _fs = fs;
        }

        private void CreateNeededDirectories()
        {
            // These both no-op if the directory already exists,
            // so no need to check ourselves
            _fs.Directory.CreateDirectory(_config.DisabledFolder);

            _fs.Directory.CreateDirectory(_config.ModsFolder);
        }

        public void Toggle(ModItem mod)
        {
            if (mod.State is not InstalledState state)
                throw new InvalidOperationException("Cannot enable mod which is not installed!");

            CreateNeededDirectories();

            var (prev, after) = !state.Enabled
                ? (_config.DisabledFolder, _config.ModsFolder)
                : (_config.ModsFolder, _config.DisabledFolder);

            (prev, after) = (
                Path.Combine(prev, mod.Name),
                Path.Combine(after, mod.Name)
            );

            // If it's already in the other state due to user usage or an error, let it fix itself.
            if (_fs.Directory.Exists(prev) && !_fs.Directory.Exists(after))
                _fs.Directory.Move(prev, after);

            mod.State = state with { Enabled = !state.Enabled };

            _installed.RecordInstalledState(mod);
        }

        /// <remarks> This enables the API if it's installed! </remarks>
        public async Task InstallApi()
        {
            await _semaphore.WaitAsync();

            try
            {
                if (_installed.ApiInstall is InstalledState { Enabled: false })
                {
                    // Don't have the toggle update it for us, as that'll infinitely loop.
                    await _ToggleApi(Update.LeaveUnchanged);
                }

                await _InstallApi(_db.Api);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task _InstallApi((string Url, int Version) manifest)
        {
            bool was_vanilla = true;

            if (_installed.ApiInstall is InstalledState { Version: var version })
            {
                if (version.Major > manifest.Version)
                    return;

                was_vanilla = false;
            }

            (string api_url, int ver) = manifest;

            string managed = _config.ManagedFolder;

            (byte[] data, string _) = await DownloadFile(api_url, _ => { });

            // Backup the vanilla assembly
            if (was_vanilla)
                _fs.File.Copy(Path.Combine(managed, Current), Path.Combine(managed, Vanilla), true);

            ExtractZip(data, managed);

            await _installed.RecordApiState(new InstalledState(true, new Version(ver, 0, 0), true));
        }

        public async Task ToggleApi()
        {
            await _semaphore.WaitAsync();

            try
            {
                await _ToggleApi();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task _ToggleApi(Update update = Update.ForceUpdate)
        {
            string managed = _config.ManagedFolder;

            Contract.Assert(_installed.ApiInstall is InstalledState);

            var st = (InstalledState) _installed.ApiInstall;

            var (move_to, move_from) = st.Enabled
                // If the api is enabled, move the current (modded) dll
                // to .m and then take from .v
                ? (Modded, Vanilla)
                // Otherwise, we're enabling the api, so move the current (vanilla) dll
                // And take from our .m file
                : (Vanilla, Modded);

            _fs.File.Move(Path.Combine(managed, Current), Path.Combine(managed, move_to));
            _fs.File.Move(Path.Combine(managed, move_from), Path.Combine(managed, Current));

            await _installed.RecordApiState(st with { Enabled = !st.Enabled });

            // If we're out of date, and re-enabling the api - update it.
            // Note we do this *after* we put the API in place.
            if (update == Update.ForceUpdate && !st.Enabled && st.Version.Major < _db.Api.Version)
                await _InstallApi(_db.Api);
        }

        public async Task Install(ModItem mod, Action<double> setProgress, bool enable)
        {
            await InstallApi();

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

            var (data, filename) = await DownloadFile(mod.Link, setProgress);

            string ext = Path.GetExtension(filename.ToLower());

            // Default to enabling
            string base_folder = enable
                ? _config.ModsFolder
                : _config.DisabledFolder;

            string mod_folder = Path.Combine(base_folder, mod.Name);

            switch (ext)
            {
                case ".zip":
                {
                    ExtractZip(data, mod_folder);

                    break;
                }

                case ".dll":
                {
                    Directory.CreateDirectory(mod_folder);

                    await _fs.File.WriteAllBytesAsync(Path.Combine(mod_folder, filename), data);

                    break;
                }

                default:
                {
                    throw new NotImplementedException($"Unknown file type for mod download: {filename}");
                }
            }

            mod.State = mod.State switch {
                InstalledState installed => installed with {
                    Version = mod.Version,
                    Updated = true,
                    Enabled = enable
                },

                NotInstalledState => new InstalledState(enable, mod.Version, true),

                _ => throw new InvalidOperationException(mod.State.GetType().Name)
            };

            await _installed.RecordInstalledState(mod);
        }

        private static async Task<(byte[] data, string filename)> DownloadFile(string uri, Action<double> setProgress)
        {
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

            byte[] data = await dl.DownloadDataTaskAsync(new Uri(uri));

            string filename = string.Empty;

            if (!string.IsNullOrEmpty(dl.ResponseHeaders?["Content-Disposition"]))
            {
                var disposition = new ContentDisposition(dl.ResponseHeaders["Content-Disposition"] ?? throw new InvalidOperationException());

                filename = disposition.FileName ?? throw new InvalidOperationException();
            }

            if (string.IsNullOrEmpty(filename))
            {
                filename = uri[(uri.LastIndexOf("/") + 1)..];
            }

            return (data, filename);
        }

        private void ExtractZip(byte[] data, string root)
        {
            using var archive = new ZipArchive(new MemoryStream(data));

            string dest_dir_path = CreateDirectoryPath(root);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string file_dest = Path.GetFullPath(Path.Combine(dest_dir_path, entry.FullName));

                if (!file_dest.StartsWith(dest_dir_path))
                    throw new IOException("Extracts outside of directory!");

                // If it's a directory:
                if (Path.GetFileName(file_dest).Length == 0)
                {
                    _fs.Directory.CreateDirectory(file_dest);
                }
                // File
                else
                {
                    // Create containing directory:
                    _fs.Directory.CreateDirectory(Path.GetDirectoryName(file_dest)!);

                    ExtractToFile(entry, file_dest);
                }
            }
        }

        private void ExtractToFile(ZipArchiveEntry src, string dest)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));

            if (dest == null)
                throw new ArgumentNullException(nameof(dest));

            // Rely on FileStream's ctor for further checking dest parameter
            const FileMode fMode = FileMode.Create;

            using (Stream fs = _fs.FileStream.Create(dest, fMode, FileAccess.Write, FileShare.None, 0x1000, false))
            {
                using (Stream es = src.Open())
                    es.CopyTo(fs);
            }

            _fs.File.SetLastWriteTime(dest, src.LastWriteTime.DateTime);
        }


        private string CreateDirectoryPath(string path)
        {
            // Note that this will give us a good DirectoryInfo even if destinationDirectoryName exists:
            IDirectoryInfo di = _fs.Directory.CreateDirectory(path);

            string dest_dir_path = di.FullName;

            if (!dest_dir_path.EndsWith(Path.DirectorySeparatorChar))
                dest_dir_path += Path.DirectorySeparatorChar;

            return dest_dir_path;
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

            try
            {
                _fs.Directory.Delete(dir, true);
            }
            catch (DirectoryNotFoundException)
            {
                /* oh well, it's uninstalled anyways */
            }

            mod.State = new NotInstalledState();

            await _installed.RecordUninstall(mod);

            if (!_config.AutoRemoveDeps)
                return;

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