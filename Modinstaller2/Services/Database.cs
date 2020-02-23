using Modinstaller2.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Modinstaller2.Services
{
    public class Database
    {
        public IEnumerable<ModItem> GetItems() => new[]
        {
            new ModItem { Enabled = true, Installed = true, Name = "QoL" }
        };
    }
}
