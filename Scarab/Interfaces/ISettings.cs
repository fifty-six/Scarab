namespace Scarab.Interfaces
{
    public interface ISettings
    {
        string ManagedFolder { get; set; }
        
        string ModsFolder { get; }
        string DisabledFolder { get; }
    }
}