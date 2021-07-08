namespace Modinstaller2.Models
{
    public abstract record ModState;

    public record InstalledState(bool Enabled, bool Updated) : ModState;

    public record NotInstalledState(bool Installing = false) : ModState;
}