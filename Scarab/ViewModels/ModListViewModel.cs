using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using JetBrains.Annotations;
using PropertyChanged.SourceGenerator;
using ReactiveUI;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.Util;

namespace Scarab.ViewModels
{
    public partial class ModListViewModel : ViewModelBase
    {
        private readonly SortableObservableCollection<ModItem> _items;
        
        private readonly ISettings _settings;
        private readonly IInstaller _installer;
        private readonly IModSource _mods;
        
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

        public ModListViewModel(ISettings settings, IModDatabase db, IInstaller inst, IModSource mods)
        {
            _settings = settings;
            _installer = inst;
            _mods = mods;

            _items = new SortableObservableCollection<ModItem>(db.Items.OrderBy(ModToOrderedTuple));

            SelectedItems = _selectedItems = _items;

            OnInstall = ReactiveCommand.CreateFromTask<ModItem>(OnInstallAsync);
            ToggleApi = ReactiveCommand.Create(ToggleApiCommand);
        }

        [UsedImplicitly]
        private IEnumerable<ModItem> FilteredItems =>
            string.IsNullOrEmpty(Search)
                ? SelectedItems
                : SelectedItems.Where(x => x.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));

        // Needed for source generator to find it.
        private void RaisePropertyChanged(string name) => IReactiveObjectExtensions.RaisePropertyChanged(this, name);

        public ReactiveCommand<ModItem, Unit> OnInstall { get; }
        
        public ReactiveCommand<Unit, Unit> ToggleApi { get; }

        public string ApiButtonText   => _mods.ApiInstall is InstalledState { Enabled: var enabled } ? (enabled ? "Disable API" : "Enable API") : "Toggle API";
        public bool   EnableApiButton => _mods.ApiInstall is InstalledState;

        private static (int priority, string name) ModToOrderedTuple(ModItem m) =>
        (
            m.State is InstalledState { Updated : false } ? -1 : 1,
            m.Name
        );

        private void ToggleApiCommand()
        {
            _installer.ToggleApi();
            
            RaisePropertyChanged(nameof(ApiButtonText));
            RaisePropertyChanged(nameof(EnableApiButton));
        }

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

        public static void Donate() => Process.Start(new ProcessStartInfo("https://paypal.me/ybham") { UseShellExecute = true });

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
            
            RaisePropertyChanged(nameof(ApiButtonText));
            RaisePropertyChanged(nameof(EnableApiButton));

            static int Comparer(ModItem x, ModItem y) => ModToOrderedTuple(x).CompareTo(ModToOrderedTuple(y));

            _items.SortBy(Comparer);
        }
    }
}