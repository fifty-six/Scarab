using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Modinstaller2
{
    [Serializable]
    public class Settings
    {
        public string ManagedFolder { get; set; }

        internal static string OSManagedSuffix = GenManagedSuffix();

        private static readonly ImmutableList<string> STATIC_PATHS = new List<string>
        {
            "Program Files/Steam/steamapps/common/Hollow Knight",
            "Program Files (x86)/Steam/steamapps/common/Hollow Knight",
            "Program Files/GOG Galaxy/Games/Hollow Knight",
            "Program Files (x86)/GOG Galaxy/Games/Hollow Knight",
            "Steam/steamapps/common/Hollow Knight",
            "GOG Galaxy/Games/Hollow Knight"
        }
        .Select(path => path.Replace('/', Path.DirectorySeparatorChar)).SelectMany(path => DriveInfo.GetDrives().Select(d => Path.Combine(d.Name, path))).ToImmutableList();

        private static readonly ImmutableList<string> USER_SUFFIX_PATHS = new List<string>
        {
            ".local/.share/Steam/steamapps/common/Hollow Knight",
            "Library/Application Support/Steam/steamapps/common/Hollow Knight/hollow_knight.app"
        }
        .Select(path => path.Replace('/', Path.DirectorySeparatorChar)).ToImmutableList();

        internal string ModsFolder     => Path.Combine(ManagedFolder, "Mods");
        internal string DisabledFolder => Path.Combine(ModsFolder, "Disabled");

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HKModInstaller",
            "HKInstallerSettings.json"
        );

        internal Settings(string path) => ManagedFolder = path;

        // Used by serializer.
        public Settings() => ManagedFolder = null!;

        public static string GetOrCreateDirPath()
        {
            string dirPath = Path.GetDirectoryName(ConfigPath) ?? throw new InvalidOperationException();
            
            // No-op if path already exists.
            Directory.CreateDirectory(dirPath);

            return dirPath;
        }

        internal static bool TryAutoDetect([MaybeNullWhen(false)] out string path)
        {
            path = STATIC_PATHS.FirstOrDefault(Directory.Exists);

            // If that's valid, use it.
            if (!string.IsNullOrEmpty(path))
                return true;

            // Otherwise, we go through the user profile suffixes.
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            path = USER_SUFFIX_PATHS
                   .Select(suffix => Path.Combine(home, suffix))
                   .FirstOrDefault(Directory.Exists);

            return !string.IsNullOrEmpty(path);
        }

        private static string GenManagedSuffix()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Path.Combine("hollow_knight_Data", "Managed");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return Path.Combine("Contents", "Resources", "Data", "Managed");
            throw new NotSupportedException();
        }

        public static Settings? Load()
        {
            if (!File.Exists(ConfigPath))
                return null;

            Debug.WriteLine($"ConfigPath: File @ {ConfigPath} exists.");

            string content = File.ReadAllText(ConfigPath);

            var settings = JsonSerializer.Deserialize<Settings>(content);

            return settings;
        }

        public static Settings Create(string path)
        {
            // Create from ManagedPath.
            var settings = new Settings(Path.Combine(path, OSManagedSuffix));

            settings.Save();

            return settings;
        }

        internal void Save()
        {
            string content = JsonSerializer.Serialize(this);

            GetOrCreateDirPath();
            
            string path = ConfigPath;
            
            File.WriteAllText(path, content);
        }
    }
}