using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Modinstaller2
{
    [Serializable]
    internal class InstallerSettings
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
            "Library/Application Support/Steam/steamapps/Hollow Knight/hollow_knight.app"
        }
        .Select(path => path.Replace('/', Path.DirectorySeparatorChar)).ToImmutableList();

        internal string ModsFolder     => Path.Combine(ManagedFolder, "Mods");
        internal string DisabledFolder => Path.Combine(ModsFolder, "Disabled");

        private static InstallerSettings _instance;

        internal static InstallerSettings Instance => _instance ?? LoadInstance();

        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HKInstallerSettings.json");

        internal static bool SettingsExists => File.Exists(ConfigPath);

        internal InstallerSettings(string path)
        {
            ManagedFolder = path;
        }

        private InstallerSettings() {}

        internal static bool TryAutoDetect(out string path)
        {
            path = STATIC_PATHS.FirstOrDefault(Directory.Exists);

            // If that's valid, use it.
            if (!string.IsNullOrEmpty(path))
                return true;

            // Otherwise, we go through the user profile suffixes.
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            path = USER_SUFFIX_PATHS.Select(suffix => Path.Combine(home, suffix)).FirstOrDefault(Directory.Exists);

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

        private static InstallerSettings LoadInstance()
        {
            if (!File.Exists(ConfigPath))
                throw new FileNotFoundException();

            Debug.WriteLine($"ConfigPath: File @ {ConfigPath} exists.");

            string content = File.ReadAllText(ConfigPath);

            return _instance = JsonSerializer.Deserialize<InstallerSettings>(content);
        }

        public static InstallerSettings CreateInstance(string path)
        {
            // Create from ManagedPath.
            _instance = new InstallerSettings(Path.Combine(path, OSManagedSuffix));

            SaveInstance();

            return _instance;
        }

        internal static void SaveInstance()
        {
            string content = JsonSerializer.Serialize(_instance);
            
            string dir = Path.GetDirectoryName(ConfigPath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(ConfigPath, content);
        }
    }
}