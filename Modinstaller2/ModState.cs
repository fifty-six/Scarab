namespace Modinstaller2
{
    public abstract record ModState { }

    public record InstalledMod(bool Enabled, bool Updated) : ModState;

    public record NotInstalledMod(bool Installing = false) : ModState { }
}