using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.HighPerformance;
using Mono.Cecil;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.Util;

namespace Scarab.Services
{
    public class HashMismatchException : Exception
    {
        /// <summary>
        /// The SHA256 value that was received
        /// </summary>
        public string Actual { get; }

        /// <summary>
        /// Expected SHA256 value
        /// </summary>
        public string Expected { get; }
        
        /// <summary>
        ///  The name of the object being checked
        /// </summary>
        public string Name { get; }
        
        public HashMismatchException(string name, string actual, string expected)
        {
            Name = name;
            Actual = actual;
            Expected = expected;
        }

    }
    
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
        
        // If we're going to have one be internal, might as well be consistent
        // ReSharper disable MemberCanBePrivate.Global 
        internal const string Modded = "Assembly-CSharp.dll.m";
        internal const string Vanilla = "Assembly-CSharp.dll.v";
        internal const string Current = "Assembly-CSharp.dll";
        // ReSharper restore MemberCanBePrivate.Global

        private readonly SemaphoreSlim _semaphore = new (1);
        private readonly HttpClient _hc;

        public Installer(ISettings config, IModSource installed, IModDatabase db, IFileSystem fs, HttpClient hc)
        {
            _config = config;
            _installed = installed;
            _db = db;
            _fs = fs;
            _hc = hc;

            try
            {
                CheckAPI().Wait();
            } catch(Exception)
            {

            }
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
            
            // Enable dependents when enabling a mod
            if (!state.Enabled) 
            {
                foreach (ModItem dep in mod.Dependencies.Select(x => _db.Items.First(i => i.Name == x)))
                {
                    if (dep.State is InstalledState { Enabled: true } or NotInstalledState)
                        continue;

                    Toggle(dep);
                }
            }

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
                await CheckAPI();
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
        private int? GetAPIVersion(string asm_name)
        {
            string asm = Path.Combine(_config.ManagedFolder, asm_name);
            if (!_fs.File.Exists(asm)) return null;
            using (AssemblyDefinition asm_definition = AssemblyDefinition.ReadAssembly(asm))
            {
                TypeDefinition? t_modhooks = asm_definition.MainModule.GetType("Modding.ModHooks");
                if (t_modhooks is null) return null;
                FieldDefinition? ver = t_modhooks.Fields.FirstOrDefault(x => x.Name == "_modVersion");
                if (ver is null || !ver.IsLiteral) throw new InvalidOperationException("Invalid ModdingAPI file");
                return (int)ver.Constant;
            }
        }
        public async Task<bool> CheckAPI()
        {
            if(!_fs.File.Exists(Path.Combine(_config.ManagedFolder, Vanilla)) || GetAPIVersion(Vanilla) != null)
            {
                _installed.HasVanilla = false;
            }
            else
            {
                _installed.HasVanilla = true;
            }
            int? cur_ver = GetAPIVersion(Current);
            bool enabled = true;
            if(cur_ver == null)
            {
                enabled = false;
                cur_ver = GetAPIVersion(Modded);
            }
            InstalledState? api_state = _installed.ApiInstall as InstalledState;
            if (api_state == null)
            {
                if (cur_ver == null) return false;
                await _installed.RecordApiState(new InstalledState(enabled, new((int)cur_ver, 0, 0), false));
                return true;
            }
            if (cur_ver == null)
            {
                await _installed.RecordApiState(new NotInstalledState());
                return false;
            }
            if(api_state.Version.Major != cur_ver || api_state.Enabled != enabled)
            {
                await _installed.RecordApiState(new InstalledState(enabled, new((int)cur_ver, 0, 0), api_state.Updated));
            }
            return true;
        }
        private async Task _InstallApi((string Url, int Version, string SHA256) manifest)
        {
            bool was_vanilla = true;
            if (await CheckAPI())
            {
                if (((InstalledState)_installed.ApiInstall).Version.Major > manifest.Version)
                    return;

                was_vanilla = false;
            }
            
            (string api_url, int ver, string hash) = manifest;

            string managed = _config.ManagedFolder;

            (ArraySegment<byte> data, string _) = await DownloadFile(api_url, _ => { });
            
            ThrowIfInvalidHash("the API", data, hash);

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
            if (!await CheckAPI()) return;

            string managed = _config.ManagedFolder;

            Contract.Assert(_installed.ApiInstall is InstalledState);

            var st = (InstalledState) _installed.ApiInstall;
            if (st.Enabled && !_installed.HasVanilla) return;
            var (move_to, move_from) = st.Enabled
                // If the api is enabled, move the current (modded) dll
                // to .m and then take from .v
                ? (Modded, Vanilla)
                // Otherwise, we're enabling the api, so move the current (vanilla) dll
                // And take from our .m file
                : (Vanilla, Modded);
            
            _fs.File.Move(Path.Combine(managed, Current), Path.Combine(managed, move_to), true);
            _fs.File.Move(Path.Combine(managed, move_from), Path.Combine(managed, Current), true);

            await _installed.RecordApiState(st with { Enabled = !st.Enabled });

            // If we're out of date, and re-enabling the api - update it.
            // Note we do this *after* we put the API in place.
            if (update == Update.ForceUpdate && !st.Enabled && st.Version.Major < _db.Api.Version)
                await _InstallApi(_db.Api);
        }

        /// <summary>
        /// Installs the given mod.
        /// </summary>
        /// <param name="mod">Mod to install</param>
        /// <param name="setProgress">Action called to indicate progress asynchronously</param>
        /// <param name="enable">Whether the mod is enabled after installation</param>
        /// <exception cref="HashMismatchException">Thrown if the download doesn't match the given hash</exception>
        public async Task Install(ModItem mod, Action<ModProgressArgs> setProgress, bool enable)
        {
            await InstallApi();

            await _semaphore.WaitAsync();

            try
            {
                CreateNeededDirectories();

                void DownloadProgressed(DownloadProgressArgs args)
                {
                    setProgress(new ModProgressArgs {
                        Download = args
                    });
                }

                // Start our progress
                setProgress(new ModProgressArgs());

                await _Install(mod, DownloadProgressed, enable);
                
                setProgress(new ModProgressArgs {
                    Completed = true
                });
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

        private async Task _Install(ModItem mod, Action<DownloadProgressArgs> setProgress, bool enable)
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

            ThrowIfInvalidHash(mod.Name, data, mod.Sha256);

            // Sometimes our filename is quoted, remove those.
            filename = filename.Trim('"');
            
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

                    await _fs.File.WriteAllBytesAsync(Path.Combine(mod_folder, filename), data.Array);

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

        private static void ThrowIfInvalidHash(string name, ArraySegment<byte> data, string modSha256)
        {
            var sha = SHA256.Create();

            byte[] hash = sha.ComputeHash(data.AsMemory().AsStream());

            string strHash = BitConverter.ToString(hash).Replace("-", string.Empty);

            if (!string.Equals(strHash, modSha256, StringComparison.OrdinalIgnoreCase))
                throw new HashMismatchException(name, actual: strHash, expected: modSha256);
        }

        private async Task<(ArraySegment<byte> data, string filename)> DownloadFile(string uri, Action<DownloadProgressArgs> setProgress)
        {
            (ArraySegment<byte> bytes, HttpResponseMessage response) = await _hc.DownloadBytesWithProgressAsync(
                new Uri(uri), 
                new Progress<DownloadProgressArgs>(setProgress)
            );

            string? filename = string.Empty;

            if (response.Content.Headers.ContentDisposition is ContentDispositionHeaderValue disposition)
                filename = disposition.FileName;

            if (string.IsNullOrEmpty(filename))
                filename = uri[(uri.LastIndexOf("/") + 1)..];

            return (bytes, filename);
        }

        private void ExtractZip(ArraySegment<byte> data, string root)
        {
            using var archive = new ZipArchive(data.AsMemory().AsStream());

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
