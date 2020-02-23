using System;
using System.IO;
using System.Text.Json;

namespace Modinstaller2
{
    class InstallerSettings
    {
        internal string ManagedFolder = "D:\\Steam\\steamapps\\common\\Hollow Knight\\hollow_knight_Data\\Managed";
        internal string ModsFolder => Path.Combine(ManagedFolder, "Mods");
        internal string DisabledFolder => Path.Combine(ModsFolder, "Disable");

        private static InstallerSettings _instance;

        internal static InstallerSettings Instance => _instance ?? LoadInstance();

        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Settings.json");

        internal static InstallerSettings LoadInstance()
        {
#if !DEBUG
            if (!File.Exists(ConfigPath))
                throw new FileNotFoundException();

            string content = File.ReadAllText(ConfigPath);

            return _instance = JsonSerializer.Deserialize<InstallerSettings>(content);
#else
            return _instance = new InstallerSettings();
#endif
        }

        internal static void SaveInstance()
        {
            string content = JsonSerializer.Serialize(_instance);

            File.WriteAllText(ConfigPath, content);
        }

    }
}
