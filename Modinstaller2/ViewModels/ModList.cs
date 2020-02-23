using Modinstaller2.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Modinstaller2.ViewModels
{
    public class ModList : ViewModelBase
    {
        public ObservableCollection<ModItem> Items { get; }

        public ModList(IEnumerable<ModItem> list)
        {
            Items = new ObservableCollection<ModItem>(list);
        }
    }
}
