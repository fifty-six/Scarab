using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;
using Scarab.Interfaces;
using Scarab.Util;

namespace Scarab
{
    [Serializable]
    public class Settings : ISettings
    {
        public string ManagedFolder { get; set; }

        public bool AutoRemoveDeps { get; }
        
        public bool RequiresWorkaroundClient { get; set; }

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
            ".var/app/ocm.valvesoftware.Steam/data/Steam/steamapps/common",
            // Symlinks to the Steam root on linux
            ".steam/steam",
            ".steam/root",
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

        internal Settings(string path) => ManagedFolder = path;

        // Used by serializer.
        public Settings()
        {
            ManagedFolder = null!;
            AutoRemoveDeps = false;
        }

        public static string GetOrCreateDirPath()
        {
            string dirPath = Path.GetDirectoryName(ConfigPath) ?? throw new InvalidOperationException();

            // No-op if path already exists.
            Directory.CreateDirectory(dirPath);

            return dirPath;
        }

        internal static bool TryAutoDetect([MaybeNullWhen(false)] out ValidPath path)
        {
            path = STATIC_PATHS.Select(PathUtil.ValidateWithSuffix).FirstOrDefault(x => x is not null);

            // If that's valid, use it.
            if (path is not null)
                return true;

            // Otherwise, we go through the user profile suffixes.
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            path = USER_SUFFIX_PATHS
                   .Select(suffix => Path.Combine(home, suffix))
                   .Select(PathUtil.ValidateWithSuffix)
                   .FirstOrDefault(x => x is not null);

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
            if (PathUtil.ValidateWithSuffix(gog_path) is not ValidPath vpath)
                return false;

            path = vpath;

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
                                .FirstOrDefault(x => x is not null);

            return path is not null;
        }

        public static Settings? Load()
        {
            if (!File.Exists(ConfigPath))
                return null;

            Debug.WriteLine($"ConfigPath: File @ {ConfigPath} exists.");

            string content = File.ReadAllText(ConfigPath);

            try
            {
                return JsonSerializer.Deserialize<Settings>(content);
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

            settings.Save();

            return settings;
        }

        public void Save()
        {
            string content = JsonSerializer.Serialize(this);

            GetOrCreateDirPath();

            string path = ConfigPath;

            File.WriteAllText(path, content);
        }
    }
}