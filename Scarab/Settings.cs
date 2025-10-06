using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Avalonia.Styling;
using Microsoft.Win32;

namespace Scarab;

[Serializable]
public class Settings : ISettings
{
    public enum GamePlatform
    {
        Linux,
        Windows,
        MacOS
    }

    public bool PlatformChanged { get; private set; }

    public string ManagedFolder { get; set; } = null!;

    public bool AutoRemoveDeps { get; }

    public bool RequiresWorkaroundClient { get; set; }

    public GamePlatform Platform { get; set; } = GetDefaultPlatform();

    public string PreferredCulture { get; set; } = CultureInfo.CurrentUICulture.Name;

    public Theme PreferredTheme { get; set; } = Theme.Dark;
    
    private static GamePlatform GetDefaultPlatform()
    {
        if (OperatingSystem.IsLinux())
            return GamePlatform.Linux;
        if (OperatingSystem.IsWindows())
            return GamePlatform.Windows;
        if (OperatingSystem.IsMacOS())
            return GamePlatform.MacOS;

        throw new NotSupportedException("Unknown platform!");
    }

    // @formatter:off
    private static readonly ImmutableList<string> STATIC_PATHS = new List<string>
    {
        "Program Files/Steam/steamapps/common/Hollow Knight",
        "Program Files (x86)/Steam/steamapps/common/Hollow Knight",
        "Program Files/GOG Galaxy/Games/Hollow Knight",
        "Program Files (x86)/GOG Galaxy/Games/Hollow Knight",
        "Steam/steamapps/common/Hollow Knight",
        "GOG Galaxy/Games/Hollow Knight",
        "XboxGames/Hollow Knight/Content"
    }
    .SelectMany(path => DriveInfo.GetDrives().Select(d => Path.Combine(d.Name, path))).ToImmutableList();

    private static readonly ImmutableList<string> USER_SUFFIX_PATHS = new List<string>
    {
        // Default locations on linux
        ".local/share/Steam/steamapps/common/Hollow Knight",
        ".steam/steam/steamapps/common/Hollow Knight",
        // Flatpak
        ".var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/Hollow Knight",
        // Symlinks to the Steam root on linux
        ".steam/root/steamapps/common/Hollow Knight",
        // Default for macOS
        "Library/Application Support/Steam/steamapps/common/Hollow Knight/hollow_knight.app"
    }
    .ToImmutableList();
    // @formatter:on

    private static string ConfigPath => Path.Combine
    (
        Environment.GetFolderPath
        (
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create
        ),
        "HKModInstaller",
        "HKInstallerSettings.json"
    );

    internal Settings(string path) : this() => ManagedFolder = path;

    // Used by serializer.
    public Settings() { }

    public static string GetOrCreateDirPath()
    {
        string dirPath = Path.GetDirectoryName(ConfigPath) ?? throw new InvalidOperationException();

        // No-op if path already exists.
        Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    internal static bool TryAutoDetect([MaybeNullWhen(false)] out ValidPath path)
    {
        path = STATIC_PATHS.Select(PathUtil.ValidateWithSuffix)
                            .OfType<ValidPath>()
                            .FirstOrDefault();

        // If that's valid, use it.
        if (path is not null)
            return true;

        // Otherwise, we go through the user profile suffixes.
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        path = USER_SUFFIX_PATHS
            .Select(suffix => Path.Combine(home, suffix))
            .Select(PathUtil.ValidateWithSuffix)
            .OfType<ValidPath>()
            .FirstOrDefault();
        
        return path is not null || TryDetectFromRegistry(out path);
    }

    private static bool TryDetectFromRegistry([MaybeNullWhen(false)] out ValidPath path)
    {
        path = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        return TryDetectSteamRegistry(out path) || TryDetectGogRegistry(out path);
    }

    [SupportedOSPlatform(nameof(OSPlatform.Windows))]
    private static bool TryDetectGogRegistry([MaybeNullWhen(false)] out ValidPath path)
    {
        path = null;

        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games\1308320804", "workingDir", null) is not string gog_path)
            return false;

        // Double check, just in case.
        if (PathUtil.ValidateWithSuffix(gog_path) is not ValidPath validPath)
            return false;

        path = validPath;

        return true;
    }

    [SupportedOSPlatform(nameof(OSPlatform.Windows))]
    private static bool TryDetectSteamRegistry([MaybeNullWhen(false)] out ValidPath path)
    {
        path = null;

        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) is not string steam_install)
            return false;

        IEnumerable<string> lines;

        try
        {
            lines = File.ReadLines(Path.Combine(steam_install, "steamapps", "libraryfolders.vdf"));
        }
        catch (Exception e) when (
            e is FileNotFoundException
                or UnauthorizedAccessException
                or IOException
                or DirectoryNotFoundException
        )
        {
            return false;
        }

        string? Parse(string line)
        {
            line = line.TrimStart();

            if (!line.StartsWith("\"path\""))
                return null;

            string[] pair = line.Split("\t", 2, StringSplitOptions.RemoveEmptyEntries);

            return pair.Length != 2
                ? null
                : pair[1].Trim('"');
        }

        IEnumerable<string> library_paths = lines.Select(Parse).OfType<string>();

        path = library_paths.Select(library_path => Path.Combine(library_path, "steamapps", "common", "Hollow Knight"))
                            .Select(PathUtil.ValidateWithSuffix)
                            .OfType<ValidPath>()
                            .FirstOrDefault();

        return path is not null;
    }

    public static Settings? Load()
    {
        if (!File.Exists(ConfigPath))
            return null;

        Log.Debug("ConfigPath: File @ {ConfigPath} exists.", ConfigPath);

        string content = File.ReadAllText(ConfigPath);

        try
        {
            var res = JsonSerializer.Deserialize<Settings>(content);
            res?.DetectLinuxGamePlatform();

            return res;
        }
        // The JSON is malformed, act as if we don't have settings as a backup
        catch (Exception e) when (e is JsonException or ArgumentNullException)
        {
            return null;
        }
    }

    public static Settings Create(string path)
    {
        // Create from ManagedPath.
        var settings = new Settings(path);
        settings.DetectLinuxGamePlatform();

        settings.Save();

        return settings;
    }

    /// <summary>
    /// If the users is using proton, swap the platform to Windows and vice versa.
    ///
    /// Called on both creation and loading of settings in case
    /// a user swaps after a previous use of the application.
    /// </summary>
    private void DetectLinuxGamePlatform()
    {
        if (GetDefaultPlatform() != GamePlatform.Linux)
            return;

        var prev = Platform;

        try
        {
            // Managed -> hollow_knight_Data -> Hollow Knight
            string @base = Path.GetFullPath(Path.Combine(ManagedFolder, "..", ".."));

            Platform = File.Exists(Path.Combine(@base, "hollow_knight.exe"))
                // We're on proton.
                ? GamePlatform.Windows
                // Native
                : GamePlatform.Linux;

            Log.Logger.Debug("Platform detected: {Platform}", Platform);

            if (prev != Platform)
            {
                Log.Logger.Information("Platform changed from {Prev} to {Platform}, forcing re-installs!", prev, Platform);
                PlatformChanged = true;
            }
        }
        catch (ArgumentException e)
        {
            Log.Logger.Error("Failure in path resolution for linux platform detection: {Path} with {Exception}",
                ManagedFolder,
                e
            );
        }
    }

    public void Save()
    {
        string content = JsonSerializer.Serialize(this);

        GetOrCreateDirPath();

        string path = ConfigPath;

        File.WriteAllText(path, content);
    }

    public void Apply()
    {
        Debug.Assert(Application.Current is not null);
        
        Application.Current.RequestedThemeVariant = PreferredTheme == Theme.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
        
        LocalizeExtension.ChangeLanguage(new CultureInfo(PreferredCulture));
    }

    public Link PlatformLink(Links links)
    {
        var link = Platform switch
        {
            GamePlatform.Linux => links.Linux,
            GamePlatform.Windows => links.Windows,
            GamePlatform.MacOS => links.Mac,
            _ => throw new ArgumentOutOfRangeException(nameof(links))
        };

        if (link is not null) 
            return link;
        
        Log.Logger.Error("Link {Links} was missing an entry!", links);
        throw new InvalidOperationException("Link for mod was missing an entry!");
    }
}