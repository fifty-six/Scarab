using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using JetBrains.Annotations;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
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
        
        public ReactiveCommand<ModItem, Unit> OnUpdate { get; }
        public ReactiveCommand<ModItem, Unit> OnInstall { get; }
        public ReactiveCommand<ModItem, Unit> OnEnable { get; }
        
        public ReactiveCommand<Unit, Unit> ToggleApi { get; }
        public ReactiveCommand<Unit, Unit> UpdateApi { get; }
        
        public ReactiveCommand<Unit, Unit> ChangePath { get; }

        public ObservableCollection<string> TagList { get; set; }

        enum ModViewState
        {
            All, Installed, Enabled, OutOfDate
        }

        private ModViewState _modViewState = ModViewState.All;
        
        
        private string _selectedTag = "All Tags";
        public string SelectedTag
        {
            get => _selectedTag;
            set
            {
                _selectedTag = value;
                DisplayModsCorrectly();
            }
        }

        private bool _normalSearch = true;
        public bool NormalSearch
        {
            get => _normalSearch;
            set
            {
                _normalSearch = value;
                RaisePropertyChanged(nameof(FilteredItems));
            }
        } 

        private bool _reverseSearch = false;
        public bool ReverseSearch
        {
            get => _reverseSearch;
            set
            {
                _reverseSearch = value;
                RaisePropertyChanged(nameof(FilteredItems));
            }
        } 

        public ModListViewModel(ISettings settings, IModDatabase db, IInstaller inst, IModSource mods)
        {
            _settings = settings;
            _installer = inst;
            _mods = mods;
            _db = db;

            _items = new SortableObservableCollection<ModItem>(db.Items.OrderBy(ModToOrderedTuple));

            SelectedItems = _selectedItems = _items;

            OnInstall = ReactiveCommand.CreateFromTask<ModItem>(OnInstallAsync);
            OnUpdate = ReactiveCommand.CreateFromTask<ModItem>(OnUpdateAsync);
            OnEnable = ReactiveCommand.CreateFromTask<ModItem>(OnEnableAsync);
            ToggleApi = ReactiveCommand.Create(ToggleApiCommand);
            ChangePath = ReactiveCommand.CreateFromTask(ChangePathAsync);
            UpdateApi = ReactiveCommand.CreateFromTask(UpdateApiAsync);
            TagList = new ObservableCollection<string>()
            {
                "All Tags", 
                "Boss", 
                "Cosmetic", 
                "Expansion", 
                "Gameplay", 
                "Library",
                "Utility"
            };
        }

        private void DisplayModsCorrectly()
        {
            IEnumerable<ModItem> newList = _items;
                
            if (SelectedTag != "All Tags")
            {
                newList = _items.Where(x =>
                {
                    if (x.Tags == null!) return false;
                    return x.Tags.Contains(SelectedTag);
                });
            }

            SelectedItems = _modViewState switch
            {
                ModViewState.All => newList,
                ModViewState.Installed => newList.Where(x => x.Installed),
                ModViewState.Enabled => newList.Where(x=> x.State is InstalledState { Enabled: true }),
                ModViewState.OutOfDate => newList.Where(x=> x.State is InstalledState { Updated: false }),
                _ => throw new InvalidOperationException("Unreachable") 
            };
        }

        [UsedImplicitly]
        private IEnumerable<ModItem> FilteredItems
        {
            get
            {
                if (NormalSearch)
                {
                    return string.IsNullOrEmpty(Search)
                        ? SelectedItems
                        : SelectedItems.Where(x => x.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
                    
                }
                else
                {
                    return string.IsNullOrEmpty(Search)
                        ? SelectedItems
                        : GetFullReverseDependencies();
                }
            }
        }
        

        private IEnumerable<ModItem> GetFullReverseDependencies()
        {
            List<ModItem> AllDependents = new();

            //check to see if the search is actually a mod or not to not waste the effort
            var searchedMod = GetMod(Search!);
            if (searchedMod != null)
            {
                foreach (var mod in SelectedItems)
                {
                    if (mod.HasIntegrations)
                    {
                        if (mod.Integrations.Contains(searchedMod.Name))
                        {
                            AllDependents.Add(mod);
                            continue;
                        }
                    }
                    if (RecursiveCheckDependency(mod, searchedMod))
                    {
                        AllDependents.Add(mod);
                    }
                }
            }
            return AllDependents;
        }

        private bool RecursiveCheckDependency(ModItem Mod, ModItem ToSearchDependencyMod)
        {
            if (!Mod.HasDependencies) return false;

            foreach (var dependency in Mod.Dependencies)
            {
                var dependencyMod = GetMod(dependency); 

                if (dependencyMod == null) continue;

                if (dependencyMod.Name.Equals(ToSearchDependencyMod.Name, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (RecursiveCheckDependency(dependencyMod, ToSearchDependencyMod)) return true;
            }

            return false;
        }
        
        private ModItem? GetMod(string name)
        {
            return _items.FirstOrDefault(x =>
                x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public string ApiButtonText => _mods.ApiInstall is InstalledState { Enabled: var enabled } ? (enabled ? "Disable API" : "Enable API") : "Toggle API";
        public string ApiButtonTooltip => _mods.ApiInstall is InstalledState { Enabled: var enabled } ? (enabled ? "Disable the modding API to make the game vanilla" : "Enable the modding API to make the game modded") : "Toggle API";
        public bool ApiOutOfDate => _mods.ApiInstall is InstalledState { Version: var v } && v.Major < _db.Api.Version;

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

        private async void ToggleApiCommand()
        {
            await _installer.ToggleApi();
            
            RaisePropertyChanged(nameof(ApiButtonText));
            RaisePropertyChanged(nameof(ApiButtonTooltip));
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

        public void SelectAll()
        {
            _modViewState = ModViewState.All;
            DisplayModsCorrectly();
        }

        public void SelectInstalled()
        {
            _modViewState = ModViewState.Installed;
            DisplayModsCorrectly();
        }

        public void SelectUnupdated()
        {
            _modViewState = ModViewState.OutOfDate;
            DisplayModsCorrectly();
        }

        public void SelectEnabled()
        {
            _modViewState = ModViewState.Enabled;
            DisplayModsCorrectly();
        }

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
            RaisePropertyChanged(nameof(ApiButtonTooltip));
            RaisePropertyChanged(nameof(EnableApiButton));
        }

        private async Task InternalUpdateInstallAsync(ModItem item, Func<IInstaller, Action<ModProgressArgs>, Task> f)
        {
            static bool IsHollowKnight(Process p) => (
                   p.ProcessName.StartsWith("hollow_knight")
                || p.ProcessName.StartsWith("Hollow Knight")
            );
            
            if (Process.GetProcesses().FirstOrDefault(IsHollowKnight) is Process proc)
            {
                var res = await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams {
                    ContentTitle = "Warning!",
                    ContentMessage = "Hollow Knight is open! This may lead to issues when installing mods. Close Hollow Knight?",
                    ButtonDefinitions = ButtonEnum.YesNo,
                    MinHeight = 200,
                    SizeToContent = SizeToContent.WidthAndHeight,
                }).Show();

                if (res == ButtonResult.Yes)
                    proc.Kill();
            }
            
            try
            {
                await f
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
            RaisePropertyChanged(nameof(ApiButtonTooltip));
            RaisePropertyChanged(nameof(EnableApiButton));
            
            static int Comparer(ModItem x, ModItem y) => ModToOrderedTuple(x).CompareTo(ModToOrderedTuple(y));
            
            _items.SortBy(Comparer);
        }

        private async Task OnUpdateAsync(ModItem item) => await InternalUpdateInstallAsync(item, item.OnUpdate);

        private async Task OnInstallAsync(ModItem item) => await InternalUpdateInstallAsync(item, item.OnInstall);

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