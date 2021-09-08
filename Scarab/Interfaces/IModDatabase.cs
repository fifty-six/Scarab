using System.Collections.Generic;
using Scarab.Models;

namespace Scarab.Interfaces
{
    public interface IModDatabase
    {
        IEnumerable<ModItem> Items { get; }
        
        (string Url, int Version) Api { get; }
    }
}