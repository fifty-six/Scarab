using System.Collections.Generic;
using Modinstaller2.Models;

namespace Modinstaller2.Interfaces
{
    public interface IModDatabase
    {
        IEnumerable<ModItem> Items { get; }
    }
}