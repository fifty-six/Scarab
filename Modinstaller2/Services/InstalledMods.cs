using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Modinstaller2.Models;

namespace Modinstaller2.Services
{
    [Serializable]
    public record InstalledMods
    {
        private const string FILE_NAME = "InstalledMods.json";

        public Dictionary<string, InstalledState> Mods { get; init; } = new();

        private static readonly string ConfigPath = Path.Combine(Settings.GetOrCreateDirPath(), FILE_NAME);

        public static InstalledMods Load() =>
            File.Exists(ConfigPath)
                ? JsonSerializer.Deserialize<InstalledMods>(File.ReadAllText(ConfigPath)) ?? throw new InvalidDataException()
                : new InstalledMods();

        public ModState FromManifest(Manifest manifest)
        {
            if (Mods.TryGetValue(manifest.Name, out var existing))
            {
                return existing with
                {
                    Updated = existing.Version >= manifest.Version.Value
                };
            }

            return new NotInstalledState();
        }

        public async Task RecordInstall(ModItem item)
        {
            Contract.Assert(item.State is InstalledState);

            Mods[item.Name] = (InstalledState) item.State;

            await SaveToDiskAsync();
        }

        public async Task RecordUninstall(ModItem item)
        {
            Contract.Assert(item.State is NotInstalledState);

            Mods.Remove(item.Name);

            await SaveToDiskAsync();
        }

        private async Task SaveToDiskAsync()
        {
            await using FileStream fs = File.Exists(ConfigPath)
                ? new FileStream(ConfigPath, FileMode.Truncate)
                : File.Create(ConfigPath);

            await JsonSerializer.SerializeAsync(fs, this);
        }
    }
}