using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        public ModState ApiInstall
        {
            get => (ModState?) _ApiState ?? new NotInstalledState();
            private set => _ApiState = value is InstalledState s ? s : null; 
        } 

        [JsonInclude]
        // public get because System.Text.Json won't let me make both private
        public InstalledState? _ApiState { get; private set; }

        private readonly IFileSystem _fs;

        public static InstalledMods Load() =>
            File.Exists(ConfigPath)
                ? JsonSerializer.Deserialize<InstalledMods>(File.ReadAllText(ConfigPath)) ?? throw new InvalidDataException()
                : new InstalledMods();

        public InstalledMods() => _fs = new FileSystem();

        public InstalledMods(IFileSystem fs) => _fs = fs;

        public async Task Reset()
        {
            Mods.Clear();
            _ApiState = null;

            await SaveToDiskAsync();
        }

        public async Task RecordApiState(ModState st)
        {
            ApiInstall = st;
            
            await SaveToDiskAsync();
        }

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
        
        public async Task RecordInstalledState(ModItem item)
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