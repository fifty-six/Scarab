using System.Collections.Generic;
using System.Collections.ObjectModel;
using Modinstaller2.Models;

namespace Modinstaller2.ViewModels
{
    public class ModListViewModel : ViewModelBase
    {
        public ObservableCollection<ModItem> Items { get; }

        public ModListViewModel(IEnumerable<ModItem> list)
        {
            Items = new ObservableCollection<ModItem>(list);
        }
    }
}
