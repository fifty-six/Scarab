using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Modinstaller2.Models;
using ReactiveUI;

namespace Modinstaller2.ViewModels
{
    public class ModListViewModel : ViewModelBase
    {
        internal SortableObservableCollection<ModItem> Items { get; }

        [UsedImplicitly]
        public ReactiveCommand<ModItem, Task> OnInstall { get; }

        public ModListViewModel(IEnumerable<ModItem> list)
        {
            Items = new SortableObservableCollection<ModItem>(list.OrderBy(x => (x.Updated ?? true ? 1 : -1, x.Name)));

            OnInstall = ReactiveCommand.Create<ModItem, Task>(OnInstallAsync);
        }

        [UsedImplicitly]
        public async Task OnInstallAsync(ModItem item)
        {
            await item.OnInstall(Items);

            Items.SortBy((x, y) => (x.Updated ?? true ? 1 : -1, x.Name).CompareTo((y.Updated ?? true ? 1 : -1, y.Name)));
        }

        [UsedImplicitly]
        private void Donate() => Process.Start(new ProcessStartInfo { FileName = "http://paypal.me/ybham", UseShellExecute = true});
    }
}
