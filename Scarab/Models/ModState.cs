using System;
using System.Text.Json.Serialization;
using Scarab.Util;

namespace Scarab.Models
{
    public abstract record ModState;

    public record InstalledState : ModState
    {
        [JsonConverter(typeof(JsonVersionConverter))]
        public Version Version { get; init; }
        
        [JsonIgnore]
        public bool Updated { get; init; }
        
        public bool Enabled { get; init; }
        
        public InstalledState(bool Enabled, Version Version, bool Updated)
        {
            this.Enabled = Enabled;
            this.Version = Version;
            this.Updated = Updated;
        }
        
        public void Deconstruct(out bool enabled, out bool updated)
        {
            enabled = Enabled;
            updated = Updated;
        }
    }

    public record NotInstalledState(bool Installing = false) : ModState;
}