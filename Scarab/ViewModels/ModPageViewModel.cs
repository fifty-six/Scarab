using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
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

namespace Scarab.ViewModels;

public partial class ModPageViewModel : ViewModelBase
{
    private readonly IModDatabase _db;

    private readonly ReadOnlyObservableCollection<ModItem> _filteredItems;
    private readonly IInstaller _installer;
    private readonly SortableObservableCollection<ModItem> _items;
    private readonly IModSource _mods;
    private readonly ReverseDependencySearch _reverseDependencySearch;

    private readonly ISettings _settings;

    public ModState Api => _mods.ApiInstall;

    [Notify("ProgressBarIndeterminate")] private bool _pbIndeterminate;

    [Notify("Progress")] private double _pbProgress;

    [Notify("ProgressBarVisible")] private bool _pbVisible;

    [Notify] private string? _search;

    [Notify] private Func<ModItem, bool> _searchFilter = _ => true;

    private ModItem? _selectedModItem;

    [Notify] private Tag _selectedTag = Tag.All;

    [Notify] private Func<ModItem, bool> _selectionFilter = _ => true;

    [Notify] private Func<ModItem, bool> _tagFilter = _ => true;

    private bool _updating;

    public ModPageViewModel(ISettings settings, IModDatabase db, IInstaller inst, IModSource mods)
    {
        _settings = settings;
        _installer = inst;
        _mods = mods;
        _db = db;

        _items = new SortableObservableCollection<ModItem>(db.Items.OrderBy(ModToOrderedTuple));

        // Create a source cache for dynamic filtering via IObservable
        var cache = new SourceCache<ModItem, string>(x => x.Name);
        cache.AddOrUpdate(_items);

        _filteredItems = new ReadOnlyObservableCollection<ModItem>(_items);

        cache.Connect()
            .Filter(this.WhenAnyValue(x => x.SelectionFilter))
            .Filter(this.WhenAnyValue(x => x.TagFilter))
            .Filter(this.WhenAnyValue(x => x.SearchFilter))
            .Sort(SortExpressionComparer<ModItem>.Ascending(x => ModToOrderedTuple(x)))
            .Bind(out _filteredItems)
            .Subscribe();

        _reverseDependencySearch = new ReverseDependencySearch(_items);

        ToggleApi = ReactiveCommand.Create(ToggleApiCommand);
        ChangePath = ReactiveCommand.CreateFromTask(ChangePathAsync);
        UpdateApi = ReactiveCommand.CreateFromTask(UpdateApiAsync);

        OnUpdate = ReactiveCommand.CreateFromTask<ModItem>(OnUpdateAsync);
        OnInstall = ReactiveCommand.CreateFromTask<ModItem>(OnInstallAsync);
        OnUninstall = ReactiveCommand.CreateFromTask<ModItem>(OnUninstallAsync);
        OnEnable = ReactiveCommand.CreateFromTask<ModItem>(OnEnableAsync);

        this.WhenAnyValue(x => x.Search)
            .Subscribe(
                s => SearchFilter = m =>
                    string.IsNullOrEmpty(s) || m.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
            );

        this.WhenAnyValue(x => x.SelectedTag).Subscribe(t => { TagFilter = m => m.Tags.HasFlag(t); });
    }

    public ReactiveCommand<Unit, Unit> ToggleApi { get; }
    public ReactiveCommand<Unit, Unit> UpdateApi { get; }

    public ReactiveCommand<Unit, Unit> ChangePath { get; }

    public ReactiveCommand<ModItem, Unit> OnUpdate { get; }
    public ReactiveCommand<ModItem, Unit> OnInstall { get; }
    public ReactiveCommand<ModItem, Unit> OnUninstall { get; }
    public ReactiveCommand<ModItem, Unit> OnEnable { get; }

    [UsedImplicitly] public ReadOnlyObservableCollection<ModItem> FilteredItems => _filteredItems;

    public ModItem? SelectedModItem
    {
        get => _selectedModItem;
        set => this.RaiseAndSetIfChanged(ref _selectedModItem, value);
    }

    public bool ApiOutOfDate => _mods.ApiInstall is InstalledState { Version: var v } && v.Major < _db.Api.Version;

    public bool CanUpdateAll => _items.Any(x => x.State is InstalledState { Updated: false }) && !_updating;

    public ImmutableArray<Tag> Tags { get; } = Enum.GetValues<Tag>().ToImmutableArray();

    private async void ToggleApiCommand()
    {
        if (_mods.ApiInstall is not InstalledState)
            await _installer.InstallApi();
        else 
            await _installer.ToggleApi();
        
        RaisePropertyChanged(nameof(Api));
    }

    private async Task ChangePathAsync()
    {
        var path = await PathUtil.SelectPathFallible();

        if (path is null)
            return;

        _settings.ManagedFolder = path;
        _settings.Save();

        await _mods.Reset();

        await MessageBoxManager.GetMessageBoxStandardWindow(Resources.MLVM_ChangePathAsync_Msgbox_Title,
            Resources.MLVM_ChangePathAsync_Msgbox_Text).Show();

        // Shutting down is easier than re-doing the source and all the items.
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    public void OpenModsDirectory()
    {
        var modsFolder = Path.Combine(_settings.ManagedFolder, "Mods");

        // Create the directory if it doesn't exist,
        // so we don't open a non-existent folder.
        Directory.CreateDirectory(modsFolder);

        Process.Start(new ProcessStartInfo(modsFolder)
        {
            UseShellExecute = true
        });
    }

    public void SelectAll()
    {
        SelectionFilter = _ => true;
    }

    public void SelectInstalled()
    {
        SelectionFilter = x => x.Installed;
    }

    public void SelectUnupdated()
    {
        SelectionFilter = x => x.State is InstalledState { Updated: false };
    }

    public void SelectEnabled()
    {
        SelectionFilter = x => x.State is InstalledState { Enabled: true };
    }

    public async void UpdateUnupdated()
    {
        _updating = false;

        RaisePropertyChanged(nameof(CanUpdateAll));

        var outOfDate = _items.Where(x => x.State is InstalledState { Updated: false }).ToList();

        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var mod in outOfDate)
        {
            // Mods can get updated as dependencies of others while doing this
            if (mod.State is not InstalledState { Updated: false })
                continue;

            await OnUpdateAsync(mod);
        }
    }

    [UsedImplicitly]
    private async Task OnEnableAsync(ModItem item)
    {
        try
        {
            var dependents = _reverseDependencySearch.GetAllEnabledDependents(item).ToList();

            if (!item.Enabled ||
                dependents.Count == 0 ||
                await DisplayHasDependentsWarning(item.Name, dependents))
                await _installer.Toggle(item);

            // to reset the visuals of the toggle to the correct value
            item.CallOnPropertyChanged(nameof(item.Enabled));
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
            await Task.Run(_installer.InstallApi);
        }
        catch (HashMismatchException e)
        {
            await DisplayHashMismatch(e);
        }
        catch (Exception e)
        {
            await DisplayGenericError("updating", "the API", e);
        }

        RaisePropertyChanged(nameof(ApiOutOfDate));
    }

    private async Task InternalUpdateInstallAsync(ModItem item, Func<IInstaller, Action<ModProgressArgs>, Task> f)
    {
        static bool IsHollowKnight(Process p)
        {
            return p.ProcessName.StartsWith("hollow_knight")
                   || p.ProcessName.StartsWith("Hollow Knight");
        }

        if (Process.GetProcesses().FirstOrDefault(IsHollowKnight) is { } proc)
        {
            var res = await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams
            {
                ContentTitle = Resources.MLVM_InternalUpdateInstallAsync_Msgbox_W_Title,
                ContentMessage = Resources.MLVM_InternalUpdateInstallAsync_Msgbox_W_Text,
                ButtonDefinitions = ButtonEnum.YesNo,
                MinHeight = 200,
                SizeToContent = SizeToContent.WidthAndHeight
            }).Show();

            try
            {
                if (res == ButtonResult.Yes)
                    proc.Kill();
            }
            catch (Win32Exception)
            {
                // tragic, but oh well.
            }
        }

        try
        {
            await Task.Run(async () => await f
            (
                _installer,
                progress =>
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        ProgressBarVisible = !progress.Completed;

                        if (progress.Download?.PercentComplete is not { } percent)
                        {
                            ProgressBarIndeterminate = true;
                            return;
                        }

                        ProgressBarIndeterminate = false;
                        Progress = percent;
                    });
                }
            ));
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

        static int Comparer(ModItem x, ModItem y)
        {
            return ModToOrderedTuple(x).CompareTo(ModToOrderedTuple(y));
        }

        _items.SortBy(Comparer);
    }

    private async Task InternalInstallWithConfirmationAsync(ModItem item,
        Func<IInstaller, Action<ModProgressArgs>, Task> f)
    {
        var dependents = _reverseDependencySearch.GetAllEnabledDependents(item).ToList();

        if (!item.Installed ||
            dependents.Count == 0 ||
            await DisplayHasDependentsWarning(item.Name, dependents))
            await InternalUpdateInstallAsync(item, f);
    }

    [UsedImplicitly]
    private async Task OnUpdateAsync(ModItem item)
    {
        await InternalUpdateInstallAsync(item, item.OnUpdate);
    }

    [UsedImplicitly]
    private async Task OnInstallAsync(ModItem item)
    {
        await InternalInstallWithConfirmationAsync(item, item.OnInstall);
    }
    
    [UsedImplicitly]
    private async Task OnUninstallAsync(ModItem item)
    {
        await InternalInstallWithConfirmationAsync(item, item.OnUninstall);
    }

    private static async Task DisplayHashMismatch(HashMismatchException e)
    {
        await MessageBoxManager.GetMessageBoxStandardWindow
        (
            Resources.MLVM_DisplayHashMismatch_Msgbox_Title,
            string.Format(Resources.MLVM_DisplayHashMismatch_Msgbox_Text, e.Name, e.Actual, e.Expected),
            icon: Icon.Error
        ).Show();
    }

    private static async Task DisplayGenericError(string action, string name, Exception e)
    {
        Trace.TraceError(e.ToString());

        await MessageBoxManager.GetMessageBoxStandardWindow
        (
            "Error!",
            $"An exception occured while {action} {name}.",
            icon: Icon.Error
        ).Show();
    }

    private static async Task DisplayNetworkError(string name, HttpRequestException e)
    {
        Trace.WriteLine($"Failed to download {name}, {e}");

        await MessageBoxManager.GetMessageBoxStandardWindow
        (
            Resources.MLVM_DisplayNetworkError_Msgbox_Title,
            string.Format(Resources.MLVM_DisplayNetworkError_Msgbox_Text, name),
            icon: Icon.Error
        ).Show();
    }

    // asks user for confirmation on whether or not they want to uninstall/disable mod.
    // returns whether or not user presses yes on the message box
    private static async Task<bool> DisplayHasDependentsWarning(string modName, IEnumerable<ModItem> dependents)
    {
        var dependentsString = string.Join(", ", dependents.Select(x => x.Name));
        var result = await MessageBoxManager.GetMessageBoxStandardWindow
        (
            "Warning! This mod is required for other mods to function!",
            $"{modName} is required for {dependentsString} to function properly. Do you still want to continue?",
            icon: Icon.Stop,
            @enum: ButtonEnum.YesNo
        ).Show();

        // return whether or not yes was clicked. Also don't remove mod when box is closed with the x
        return result.HasFlag(ButtonResult.Yes) && !result.HasFlag(ButtonResult.None);
    }

    private static (int priority, string name) ModToOrderedTuple(ModItem m)
    {
        return (
            m.State is InstalledState { Updated : false } ? -1 : 1,
            m.Name
        );
    }
}