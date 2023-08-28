namespace Scarab.Interfaces;

public interface IModDatabase
{
    IEnumerable<ModItem> Items { get; }
        
    (string Url, int Version, string SHA256) Api { get; }
}