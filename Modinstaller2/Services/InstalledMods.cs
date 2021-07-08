using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Modinstaller2.Models;

namespace Modinstaller2.Services
{
    [Serializable]
    public record InstalledMods
    {
        private const string FILE_NAME = "InstalledMods.json";

        public Dictionary<string, InstalledMod> Mods { get; init; } = new();

        public static InstalledMods Load()
        {
            string dir = Settings.GetOrCreateDirPath();
            string path = Path.Combine(dir, FILE_NAME);
            
            return File.Exists(path) 
                ? JsonSerializer.Deserialize<InstalledMods>(path) 
                : new InstalledMods();
        }
    }
}