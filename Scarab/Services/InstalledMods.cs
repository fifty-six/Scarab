using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading.Tasks;
using Scarab.Interfaces;
using Scarab.Models;

namespace Scarab.Services
{
    [Serializable]
    public record InstalledMods : IModSource
    {
        private const string FILE_NAME = "InstalledMods.json";
        
        internal static readonly string ConfigPath = Path.Combine(Settings.GetOrCreateDirPath(), FILE_NAME);

        public Dictionary<string, InstalledState> Mods { get; init; } = new();

        private readonly IFileSystem _fs;

        public static InstalledMods Load() =>
            File.Exists(ConfigPath)
                ? JsonSerializer.Deserialize<InstalledMods>(File.ReadAllText(ConfigPath)) ?? throw new InvalidDataException()
                : new InstalledMods(new FileSystem());

        public InstalledMods(IFileSystem fs) => _fs = fs;

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
            await using Stream fs = _fs.File.Exists(ConfigPath)
                ? _fs.FileStream.Create(ConfigPath, FileMode.Truncate)
                : _fs.File.Create(ConfigPath);

            await JsonSerializer.SerializeAsync(fs, this);
        }
    }
}