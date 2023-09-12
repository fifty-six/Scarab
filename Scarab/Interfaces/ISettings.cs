namespace Scarab.Interfaces;

public interface ISettings
{
    bool AutoRemoveDeps { get; }
        
    string ManagedFolder { get; set; }
        
    string PreferredCulture { get; set; }
    
    Theme PreferredTheme { get; set; }
        
    string ModsFolder     => Path.Combine(ManagedFolder, "Mods");
    string DisabledFolder => Path.Combine(ModsFolder, "Disabled");

    void Save();

    void Apply();
}