using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Toolkit.HighPerformance;

namespace Scarab.Services;

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
    }

    private void CreateNeededDirectories()
    {
        // These both no-op if the directory already exists,
        // so no need to check ourselves
        _fs.Directory.CreateDirectory(_config.DisabledFolder);

        _fs.Directory.CreateDirectory(_config.ModsFolder);
    }

    public async Task Toggle(ModItem mod)
    {
        await _semaphore.WaitAsync();
            
        try
        {
            await _Toggle(mod);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task _Toggle(ModItem mod)
    {
        if (mod.State is not InstalledState state)
            throw new InvalidOperationException("Cannot enable mod which is not installed!");
            
        // Enable dependents when enabling a mod
        if (!state.Enabled) 
        {
            foreach (var dep in mod.Dependencies.Select(x => _db.Items.First(i => i.Name == x)))
            {
                if (dep.State is InstalledState { Enabled: true } or NotInstalledState)
                    continue;

                await _Toggle(dep);
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

        await _installed.RecordInstalledState(mod);
    }

    /// <remarks> This enables the API if it's installed! </remarks>
    public async Task InstallApi(IInstaller.ReinstallPolicy policy = IInstaller.ReinstallPolicy.SkipUpToDate)
    {
        await _semaphore.WaitAsync();

        try
        {
            if (_installed.ApiInstall is InstalledState { Enabled: false })
            {
                // Don't have the toggle update it for us, as that'll infinitely loop.
                await _ToggleApi(Update.LeaveUnchanged);
            }

            await _InstallApi(_db.Api, policy);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task _InstallApi(
        (Links Link, int Version) manifest,
        IInstaller.ReinstallPolicy policy = IInstaller.ReinstallPolicy.SkipUpToDate
    )
    {
        var was_vanilla = true;

        if (_installed.ApiInstall is InstalledState { Version: var version } && policy is not IInstaller.ReinstallPolicy.ForceReinstall)
        {
            if (version.Major >= manifest.Version)
                return;

            was_vanilla = false;
        }
            
        (var links, var ver) = manifest;
        var url = _config.PlatformLink(links);

        var managed = _config.ManagedFolder;

        (var data, var _) = await DownloadFile(url.URL, _ => { });
            
        ThrowIfInvalidHash("the API", data, url.SHA256);

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

    // TODO: UI
    public async Task HandlePlatformChange()
    {
        if (!_config.PlatformChanged)
            return;

        // Otherwise, we need to reinstall *the api* and some mods with platform-specific assets.
        if (_installed.ApiInstall is InstalledState st)
        {
            Log.Logger.Information("Platform changed, reinstalling API.");
        
            await InstallApi(policy: IInstaller.ReinstallPolicy.ForceReinstall);

            // Put it back where it was as InstallApi currently enables it.
            if (!st.Enabled)
                await _ToggleApi(Update.LeaveUnchanged);
        }

        foreach (var mod in _db.Items.Where(i => i.Installed))
        {
            if (mod is not { Installed: true, Link.HasPlatformSpecificLink: true, Enabled: var enabled }) 
                continue;
            
            Log.Logger.Information("Reinstalling {Mod} with platform-specific links.", mod);
                
            await Install(mod, _ => { }, enabled);
        }

        _config.Save();
    }

    private async Task _ToggleApi(Update update = Update.ForceUpdate)
    {
        var managed = _config.ManagedFolder;

        Contract.Assert(_installed.ApiInstall is InstalledState);

        var st = (InstalledState) _installed.ApiInstall;

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
        foreach (var dep in mod.Dependencies.Select(x => _db.Items.First(i => i.Name == x)))
        {
            if (dep.State is InstalledState { Updated: true, Enabled: var enabled })
            {
                if (!enabled && enable)
                    await _Toggle(dep);
                    
                continue;
            }

            // Enable the dependencies' dependencies if we're enabling this mod
            // Or if the dependency was previously not installed.
            await _Install(dep, _ => { }, enable || dep.State is NotInstalledState);
        }

        var link = _config.PlatformLink(mod.Link);

        var (data, filename) = await DownloadFile(link.URL, setProgress);

        ThrowIfInvalidHash(mod.Name, data, link.SHA256);

        // Sometimes our filename is quoted, remove those.
        filename = filename.Trim('"');
            
        var ext = Path.GetExtension(filename.ToLower());

        // Default to enabling
        var base_folder = enable
            ? _config.ModsFolder
            : _config.DisabledFolder;

        var mod_folder = Path.Combine(base_folder, mod.Name);

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

                Debug.Assert(data.Array != null, "data.Array != null");
                await _fs.File.WriteAllBytesAsync(Path.Combine(mod_folder, filename), data.Array);

                break;
            }

            default:
            {
                throw new NotImplementedException($"Unknown file type for mod download: {filename}");
            }
        }

        mod.State = mod.State switch {
            InstalledState => new InstalledState(
                Version: mod.Version,
                Updated:  true,
                Enabled: enable
            ),

            NotInstalledState => new InstalledState(enable, mod.Version, true),

            _ => throw new InvalidOperationException(mod.State.GetType().Name)
        };

        await _installed.RecordInstalledState(mod);
    }

    private static void ThrowIfInvalidHash(string name, ArraySegment<byte> data, string modSha256)
    {
        var sha = SHA256.Create();

        var hash = sha.ComputeHash(data.AsMemory().AsStream());

        var strHash = BitConverter.ToString(hash).Replace("-", string.Empty);

        if (!string.Equals(strHash, modSha256, StringComparison.OrdinalIgnoreCase))
            throw new HashMismatchException(name, actual: strHash, expected: modSha256);
    }

    private async Task<(ArraySegment<byte> data, string filename)> DownloadFile(string uri, Action<DownloadProgressArgs> setProgress)
    {
        (var bytes, var response) = await _hc.DownloadBytesWithProgressAsync(
            new Uri(uri), 
            new Progress<DownloadProgressArgs>(setProgress)
        );

        var filename = string.Empty;

        if (response.Content.Headers.ContentDisposition is { } disposition)
            filename = disposition.FileName;

        if (string.IsNullOrEmpty(filename))
            filename = uri[(uri.LastIndexOf("/", StringComparison.Ordinal) + 1)..];

        return (bytes, filename);
    }

    private void ExtractZip(ArraySegment<byte> data, string root)
    {
        using var archive = new ZipArchive(data.AsMemory().AsStream());

        var dest_dir_path = CreateDirectoryPath(root);

        foreach (var entry in archive.Entries)
        {
            var file_dest = Path.GetFullPath(Path.Combine(dest_dir_path, entry.FullName));

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

        using (Stream fs = _fs.FileStream.New(dest, fMode, FileAccess.Write, FileShare.None, 0x1000, false))
        {
            using (var es = src.Open())
                es.CopyTo(fs);
        }

        _fs.File.SetLastWriteTime(dest, src.LastWriteTime.DateTime);
    }


    private string CreateDirectoryPath(string path)
    {
        // Note that this will give us a good DirectoryInfo even if destinationDirectoryName exists:
        var di = _fs.Directory.CreateDirectory(path);

        var dest_dir_path = di.FullName;

        if (!dest_dir_path.EndsWith(Path.DirectorySeparatorChar))
            dest_dir_path += Path.DirectorySeparatorChar;

        return dest_dir_path;
    }

    private async Task _Uninstall(ModItem mod)
    {
        var dir = Path.Combine
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

        foreach (var dep in mod.Dependencies.Select(x => _db.Items.First(i => x == i.Name)))
        {
            // Make sure no other mods depend on it
            if (_db.Items.Where(x => x.State is InstalledState && x != mod).Any(x => x.Dependencies.Contains(dep.Name)))
                continue;

            await _Uninstall(dep);

            dep.State = new NotInstalledState();
        }
    }
}