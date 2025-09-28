namespace Scarab.Interfaces;

public interface IModDatabase
{
    IEnumerable<ModItem> Items { get; }

    (Links Link, int Version) Api { get; }
}