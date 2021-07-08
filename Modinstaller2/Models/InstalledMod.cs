using System.Collections.Generic;

namespace Modinstaller2.Models
{
    public record InstalledMod(IEnumerable<string> files, string name, InstalledState state);
}