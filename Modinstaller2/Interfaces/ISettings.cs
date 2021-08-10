namespace Modinstaller2.Interfaces
{
    public interface ISettings
    {
        string ManagedFolder { get; set; }
        
        string ModsFolder { get; }
        string DisabledFolder { get; }
    }
}