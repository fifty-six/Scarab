using System;
using System.Text.Json.Serialization;
using Scarab.Util;

namespace Scarab.Models
{
    public abstract record ModState;

    public record InstalledState(
        bool Enabled,
        //
        [property: JsonConverter(typeof(JsonVersionConverter))]
        Version Version,
        //
        [property: JsonIgnore] bool Updated
    ) : ModState;

    public record NotInstalledState(bool Installing = false) : ModState;
}