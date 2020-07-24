using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Modinstaller2.Models;

namespace Modinstaller2.ViewModels
{
    public class ModListViewModel : ViewModelBase
    {
        internal SortableObservableCollection<ModItem> Items { get; }

        public ModListViewModel(IEnumerable<ModItem> list)
        {
            Items = new SortableObservableCollection<ModItem>(list.OrderBy(x => (x.Updated ?? true ? 1 : -1, x.Name)));
        }

        [UsedImplicitly]
        public void OnInstall(ModItem item)
        {
            item.OnInstall(Items);

            Items.SortBy((x, y) => (x.Updated ?? true ? 1 : -1, x.Name).CompareTo((y.Updated ?? true ? 1 : -1, y.Name)));
        }
    }
}
