using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using JetBrains.Annotations;
using MessageBox.Avalonia;
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
        private readonly IModDatabase _db;
        
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
            _db = db;

            _items = new SortableObservableCollection<ModItem>(db.Items.OrderBy(ModToOrderedTuple));

            SelectedItems = _selectedItems = _items;

            OnInstall = ReactiveCommand.CreateFromTask<ModItem>(OnInstallAsync);
            ToggleApi = ReactiveCommand.Create(ToggleApiCommand);
            ChangePath = ReactiveCommand.CreateFromTask(ChangePathAsync);
            UpdateApi = ReactiveCommand.CreateFromTask(UpdateApiAsync);
        }

        [UsedImplicitly]
        private IEnumerable<ModItem> FilteredItems =>
            string.IsNullOrEmpty(Search)
                ? SelectedItems
                : SelectedItems.Where(x => x.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));

        public string ApiButtonText   => _mods.ApiInstall is InstalledState { Enabled: var enabled } ? (enabled ? "Disable API" : "Enable API") : "Toggle API";
        public bool   EnableApiButton => _mods.ApiInstall is InstalledState;
        public bool   ApiOutOfDate    => _mods.ApiInstall is InstalledState { Version: var v } && v.Major < _db.Api.Version;

        // Needed for source generator to find it.
        private void RaisePropertyChanged(string name) => IReactiveObjectExtensions.RaisePropertyChanged(this, name);

        public ReactiveCommand<ModItem, Unit> OnInstall { get; }
        
        public ReactiveCommand<Unit, Unit> ToggleApi { get; }
        
        public ReactiveCommand<Unit, Unit> ChangePath { get; }

        public ReactiveCommand<Unit, Unit> UpdateApi { get; }

        private async void ToggleApiCommand()
        {
            await _installer.ToggleApi();
            
            RaisePropertyChanged(nameof(ApiButtonText));
            RaisePropertyChanged(nameof(EnableApiButton));
        }
        
        private async Task ChangePathAsync()
        {
            string path;
            
            try
            {
                path = await PathUtil.SelectPath(fail: true);
            }
            catch (PathUtil.PathInvalidOrUnselectedException)
            {
                return;
            }

            _settings.ManagedFolder = path;
            _settings.Save();

            await _mods.Reset();

            await MessageBoxManager.GetMessageBoxStandardWindow("Changed path!", "Re-open the installer to use your new path.").Show();
            
            // Shutting down is easier than re-doing the source and all the items.
            (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).Shutdown();
        }

        public void OpenModsDirectory() => Process.Start(new ProcessStartInfo(Path.Combine(_settings.ManagedFolder, "Mods")) {
            UseShellExecute = true
        });

        public static void Donate() => Process.Start(new ProcessStartInfo("https://paypal.me/ybham") { UseShellExecute = true });
        
        public void SelectAll() => SelectedItems = _items;

        public void SelectInstalled() => SelectedItems = _items.Where(x => x.Installed);

        public void SelectUnupdated() => SelectedItems = _items.Where(x => x.State is InstalledState { Updated: false });

        public void SelectEnabled() => SelectedItems = _items.Where(x => x.State is InstalledState { Enabled: true });

        public void OnEnable(ModItem item) => _installer.Toggle(item);

        private async Task UpdateApiAsync()
        {
            await _installer.InstallApi();
            
            RaisePropertyChanged(nameof(ApiOutOfDate));
            RaisePropertyChanged(nameof(ApiButtonText));
            RaisePropertyChanged(nameof(EnableApiButton));
        }

        private async Task OnInstallAsync(ModItem item)
        {
            await item.OnInstall
            (
                _installer,
                progress =>
                {
                    ProgressBarVisible = !progress.Completed;

                    if (progress.Download?.PercentComplete is not double percent)
                    {
                        ProgressBarIndeterminate = true;
                        return;
                    }

                    ProgressBarIndeterminate = false;
                    Progress = percent;
                }
            );
            
            RaisePropertyChanged(nameof(ApiButtonText));
            RaisePropertyChanged(nameof(EnableApiButton));

            static int Comparer(ModItem x, ModItem y) => ModToOrderedTuple(x).CompareTo(ModToOrderedTuple(y));

            _items.SortBy(Comparer);
        }
        
        private static (int priority, string name) ModToOrderedTuple(ModItem m) =>
        (
            m.State is InstalledState { Updated : false } ? -1 : 1,
            m.Name
        );
        
    }
}