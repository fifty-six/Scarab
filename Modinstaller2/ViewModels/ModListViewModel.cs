using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Modinstaller2.Interfaces;
using Modinstaller2.Models;
using Modinstaller2.Services;
using Modinstaller2.Util;
using PropertyChanged.SourceGenerator;
using ReactiveUI;

namespace Modinstaller2.ViewModels
{
    public partial class ModListViewModel : ViewModelBase
    {
        private readonly SortableObservableCollection<ModItem> _items;
        private readonly ISettings _settings;
        private readonly IInstaller _installer;
        
        [Notify("ProgressBarVisible")]
        private bool _pbVisible;

        [Notify("ProgressBarIndeterminate")]
        private bool _pbIndeterminate;

        [Notify("Progress")]
        private double _pbProgress;

        [Notify]
        private IEnumerable<ModItem> _selectedItems;

        [Notify]
        private string? _search;
        
        public ModListViewModel(ISettings settings, IModDatabase db, IInstaller inst)
        {
            _settings = settings;
            _installer = inst;

            _items = new SortableObservableCollection<ModItem>(db.Items.OrderBy(ModToOrderedTuple));

            SelectedItems = _selectedItems = _items;

            OnInstall = ReactiveCommand.CreateFromTask<ModItem>(OnInstallAsync);
        }


        [UsedImplicitly]
        private IEnumerable<ModItem> FilteredItems =>
            string.IsNullOrEmpty(Search)
                ? SelectedItems
                : SelectedItems.Where(x => x.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));

        // Needed for source generator to find it.
        private void RaisePropertyChanged(string name) => IReactiveObjectExtensions.RaisePropertyChanged(this, name);

        [UsedImplicitly]
        public ReactiveCommand<ModItem, Unit> OnInstall { get; }

        private static (int priority, string name) ModToOrderedTuple(ModItem m) =>
        (
            m.State is InstalledState { Updated : false } ? -1 : 1,
            m.Name
        );

        public void SelectAll() => SelectedItems = _items;

        public void SelectInstalled() => SelectedItems = _items.Where(x => x.Installed);

        public void OpenModsDirectory() =>
            Process.Start
            (
                new ProcessStartInfo(Path.Combine(_settings.ManagedFolder, "Mods"))
                {
                    UseShellExecute = true
                }
            );

        public void SelectUnupdated() => SelectedItems = _items.Where(x => x.State is InstalledState { Updated: false });

        public void SelectEnabled() => SelectedItems = _items.Where(x => x.State is InstalledState { Enabled: true });

        [UsedImplicitly]
        public void OnEnable(ModItem item) => _installer.Toggle(item);

        [UsedImplicitly]
        public async Task OnInstallAsync(ModItem item)
        {
            await item.OnInstall
            (
                _installer,
                val => ProgressBarVisible = val,
                progress =>
                {
                    ProgressBarIndeterminate = progress < 0;

                    if (progress >= 0)
                        Progress = progress;
                }
            );

            static int Comparer(ModItem x, ModItem y) => ModToOrderedTuple(x).CompareTo(ModToOrderedTuple(y));

            _items.SortBy(Comparer);
        }
    }
}