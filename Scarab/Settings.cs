using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using Scarab.Interfaces;

namespace Scarab
{
    [Serializable]
    public class Settings : ISettings
    {
        public string ManagedFolder { get; set; }
        
        public bool AutoRemoveDeps { get; }

        private static readonly ImmutableList<string> STATIC_PATHS = new List<string>
        {
            "Program Files/Steam/steamapps/common/Hollow Knight",
            "Program Files (x86)/Steam/steamapps/common/Hollow Knight",
            "Program Files/GOG Galaxy/Games/Hollow Knight",
            "Program Files (x86)/GOG Galaxy/Games/Hollow Knight",
            "Steam/steamapps/common/Hollow Knight",
            "GOG Galaxy/Games/Hollow Knight"
        }
        .SelectMany(path => DriveInfo.GetDrives().Select(d => Path.Combine(d.Name, path))).ToImmutableList();

        private static readonly ImmutableList<string> USER_SUFFIX_PATHS = new List<string>
        {
            ".local/.share/Steam/steamapps/common/Hollow Knight",
            "Library/Application Support/Steam/steamapps/common/Hollow Knight/hollow_knight.app"
        }
        .ToImmutableList();

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
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