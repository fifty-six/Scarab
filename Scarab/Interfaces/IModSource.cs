namespace Scarab.Interfaces;

public interface IModSource
{
    Dictionary<string, InstalledState> Mods { get; }
    
    ModState ApiInstall { get; }

    Task RecordApiState(ModState st);

    ModState FromManifest(Manifest manifest);

    Task RecordInstalledState(ModItem item);

    Task RecordUninstall(ModItem item);

    Task Reset();
}