using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Modinstaller2
{
    internal class InstallerSettings
    {
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

        private static readonly ImmutableList<string> USER_SUFFIX_PATHS = new List<string>()
        {
            ".local/.share/Steam/steamapps/common/Hollow Knight",
            ".local/.share/Steam/steamapps/common/Hollow Knight",
        }
        .Select(path => path.Replace('/', Path.DirectorySeparatorChar)).ToImmutableList();

        internal readonly string ManagedFolder;
        internal string ModsFolder     => Path.Combine(ManagedFolder, "Mods");
        internal string DisabledFolder => Path.Combine(ModsFolder, "Disabled");

        private static InstallerSettings _instance;

        internal static InstallerSettings Instance => _instance ?? LoadInstance();

        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Settings.json");

        internal static bool SettingsExists => File.Exists(ConfigPath);

        internal InstallerSettings(string path)
        {
            ManagedFolder = path;
        }

        internal static bool TryAutoDetect(out string path)
        {
            path = STATIC_PATHS.FirstOrDefault(Directory.Exists);

            // If that's valid, use it.
            if (!string.IsNullOrEmpty(path)) 
            {
                return true;
            }

            // Otherwise, we go through the user profile suffixes.
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            path = USER_SUFFIX_PATHS.Select(suffix => Path.Combine(home, suffix)).FirstOrDefault(Directory.Exists);

#warning TODO: Append the actual OS-dependant path to Managed based on the found path if there is one

            return !string.IsNullOrEmpty(path);
        }

        internal static string OSManagedSuffix;

        static InstallerSettings()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                OSManagedSuffix = Path.Combine("hollow_knight_Data", "Managed");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                OSManagedSuffix = Path.Combine("Contents", "Resources", "Data", "Managed");
            else
                throw new NotSupportedException();
        }

        private static InstallerSettings LoadInstance()
        {
            if (!File.Exists(ConfigPath))
                throw new FileNotFoundException();

            string content = File.ReadAllText(ConfigPath);

            return _instance = JsonSerializer.Deserialize<InstallerSettings>(content);
        }

        internal static void SaveInstance()
        {
            string content = JsonSerializer.Serialize(_instance);

            File.WriteAllText(ConfigPath, content);
        }
    }
}