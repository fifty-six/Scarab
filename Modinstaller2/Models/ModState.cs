namespace Modinstaller2.Models
{
    public abstract record ModState;

    public record InstalledMod(bool Enabled, bool Updated) : ModState;

    public record NotInstalledMod(bool Installing = false) : ModState;
}