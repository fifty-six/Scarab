using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Modinstaller2.Models;
using ReactiveUI;

namespace Modinstaller2.ViewModels
{
    public class ModListViewModel : ViewModelBase
    {
        private bool _pbVisible;
        
        [UsedImplicitly]
        public bool ProgressBarVisible
        {
            get => _pbVisible;
            
            private set => this.RaiseAndSetIfChanged(ref _pbVisible, value);
        }

        private double _pbProgress;

        [UsedImplicitly]
        public double Progress
        {
            get => _pbProgress;

            private set => this.RaiseAndSetIfChanged(ref _pbProgress, value);
        }

        private SortableObservableCollection<ModItem> Items { get; }
        
        private IEnumerable<ModItem> FilteredItems
        {
            get;
            set;
        }

        public void FilterItems(string search)
        {
            FilteredItems = string.IsNullOrEmpty(search) 
                ? Items 
                : Items.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

            this.RaisePropertyChanged(nameof(FilteredItems));
        }
        
        [UsedImplicitly]
        public ReactiveCommand<ModItem, Task> OnInstall { get; }

        private static (int priority, string name) ModToOrderedTuple(ModItem m) =>
        (
            m.State is InstalledMod { Updated : false } ? -1 : 1,
            m.Name
        );
        
        
        public ModListViewModel(IEnumerable<ModItem> list)
        {
            Items = new SortableObservableCollection<ModItem>(list.OrderBy(ModToOrderedTuple));

            FilteredItems = Items;

            OnInstall = ReactiveCommand.Create<ModItem, Task>(OnInstallAsync);
        }

        [UsedImplicitly]
        public async Task OnInstallAsync(ModItem item)
        {
            await item.OnInstall(Items, val => ProgressBarVisible = val, progress => Progress = progress);

            static int Comparer(ModItem x, ModItem y) => ModToOrderedTuple(x).CompareTo(ModToOrderedTuple(y));
            
            Items.SortBy(Comparer);
        }
    }
}
