using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using JetBrains.Annotations;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using PropertyChanged.SourceGenerator;
using ReactiveUI;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.Services;
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
            OnEnable = ReactiveCommand.CreateFromTask<ModItem>(OnEnableAsync);
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
        public bool   ApiOutOfDate    => _mods.ApiInstall is InstalledState { Version: var v } && v.Major < _db.Api.Version;

        public bool EnableApiButton => _mods.ApiInstall switch
        {
            NotInstalledState => false,
            // Disabling, so we're putting back the vanilla assembly
            InstalledState { Enabled: true } => File.Exists(Path.Combine(_settings.ManagedFolder, Installer.Vanilla)),
            // Enabling, so take the modded one.
            InstalledState { Enabled: false } => File.Exists(Path.Combine(_settings.ManagedFolder, Installer.Modded)),
            // Unreachable
            _ => throw new InvalidOperationException()
        };

        // Needed for source generator to find it.
        private void RaisePropertyChanged(string name) => IReactiveObjectExtensions.RaisePropertyChanged(this, name);

        public ReactiveCommand<ModItem, Unit> OnInstall { get; }
        
        public ReactiveCommand<ModItem, Unit> OnEnable { get; }
        
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
            string? path = await PathUtil.SelectPathFailable();

            if (path is null)
                return;

            _settings.ManagedFolder = path;
            _settings.Save();

            await _mods.Reset();

            await MessageBoxManager.GetMessageBoxStandardWindow("Changed path!", "Re-open the installer to use your new path.").Show();
            
            // Shutting down is easier than re-doing the source and all the items.
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        }

        public void OpenModsDirectory()
        {
            var modsFolder = Path.Combine(_settings.ManagedFolder, "Mods");

            // Create the directory if it doesn't exist,
            // so we don't open a non-existent folder.
            Directory.CreateDirectory(modsFolder);
            
            Process.Start(new ProcessStartInfo(modsFolder) {
                    UseShellExecute = true
            });
        }

        public static void Donate() => Process.Start(new ProcessStartInfo("https://paypal.me/ybham") { UseShellExecute = true });
        
        public void SelectAll() => SelectedItems = _items;

        public void SelectInstalled() => SelectedItems = _items.Where(x => x.Installed);

        public void SelectUnupdated() => SelectedItems = _items.Where(x => x.State is InstalledState { Updated: false });

        public void SelectEnabled() => SelectedItems = _items.Where(x => x.State is InstalledState { Enabled: true });

        private async Task OnEnableAsync(ModItem item)
        {
            try
            {
                _installer.Toggle(item);
            }
            catch (Exception e)
            {
                await DisplayGenericError("toggling", item.Name, e);
            }
        }

        private async Task UpdateApiAsync()
        {
            try
            {
                await _installer.InstallApi();
            }
            catch (HashMismatchException e)
            {
                await DisplayHashMismatch(e);
            }
            catch (Exception e)
            {
                await DisplayGenericError("updating", name: "the API", e);
            }

            RaisePropertyChanged(nameof(ApiOutOfDate));
            RaisePropertyChanged(nameof(ApiButtonText));
            RaisePropertyChanged(nameof(EnableApiButton));
        }

        private async Task OnInstallAsync(ModItem item)
        {
            try 
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
            }
            catch (HashMismatchException e)
            {
                Trace.WriteLine($"Mod {item.Name} had a hash mismatch! Expected: {e.Expected}, got {e.Actual}");
                await DisplayHashMismatch(e);
            }
            catch (HttpRequestException e)
            {
                await DisplayNetworkError(item.Name, e);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to install mod {item.Name}. State = {item.State}, Link = {item.Link}");
                await DisplayGenericError("installing or uninstalling", item.Name, e);
            }

            // Even if we threw, stop the progress bar.
            ProgressBarVisible = false;
            
            RaisePropertyChanged(nameof(ApiButtonText));
            RaisePropertyChanged(nameof(EnableApiButton));

            static int Comparer(ModItem x, ModItem y) => ModToOrderedTuple(x).CompareTo(ModToOrderedTuple(y));

            _items.SortBy(Comparer);
        }

        private static async Task DisplayHashMismatch(HashMismatchException e)
        {
            await MessageBoxManager.GetMessageBoxStandardWindow
            (
                title: "Hash mismatch!",
                text: $"The download hash for {e.Name} ({e.Actual}) didn't match the given signature ({e.Expected}). It is either corrupt or the entry is incorrect.",
                icon: Icon.Error
            ).Show();
        }

        private static async Task DisplayGenericError(string action, string name, Exception e)
        {
            Trace.TraceError(e.ToString());

            await MessageBoxManager.GetMessageBoxStandardWindow
            (
                title: "Error!",
                text: $"An exception occured while {action} {name}.",
                icon: Icon.Error
            ).Show();
        }

        private static async Task DisplayNetworkError(string name, HttpRequestException e)
        {
            Trace.WriteLine($"Failed to download {name}, {e}");

            await MessageBoxManager.GetMessageBoxStandardWindow
            (
                title: "Network Error",
                text: $"Unable to download {name}! The site may be down or you may lack network connectivity.",
                icon: Icon.Error
            ).Show();
        }

        private static (int priority, string name) ModToOrderedTuple(ModItem m) =>
        (
            m.State is InstalledState { Updated : false } ? -1 : 1,
            m.Name
        );
        
    }
}