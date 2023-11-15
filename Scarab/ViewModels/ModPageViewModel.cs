using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;

namespace Scarab.ViewModels;

public partial class ModPageViewModel : ViewModelBase
{
    public enum ModAction
    {
        Install,
        Update,
        Uninstall,
        Toggle
    }

    private static readonly Func<object, bool> ConstTrue = _ => true;
    public static ImmutableArray<Tag> Tags { get; } = Enum.GetValues<Tag>().ToImmutableArray();
    
    private readonly ReadOnlyObservableCollection<ModItem> _filteredItems;
    private readonly SortableObservableCollection<ModItem> _items;

    private readonly Dictionary<ModItem, (string? branch, string content)?> _readmes = new();
    private readonly HttpClient _hc;
    private readonly IModDatabase _db;
    private readonly IInstaller _installer;
    private readonly IModSource _mods;
    private readonly ISettings _settings;
    private readonly ReverseDependencySearch _reverseDependencySearch;
    
    public delegate void ExceptionHandler(ModAction act, Exception e, ModItem? mod);

    public delegate void CompletionHandler(ModAction act, ModItem mod);

    public event CompletionHandler? CompletedAction;
    public event ExceptionHandler? ExceptionRaised;
    
    public ReactiveCommand<Unit, Unit> UpdateAll { get; }
    public ReactiveCommand<Unit, Unit> ToggleApi { get; }
    public ReactiveCommand<Unit, Unit> ReinstallApi { get; }
    public ReactiveCommand<Unit, Unit> UpdateApi { get; }

    public ReactiveCommand<ModItem, Unit> OnUpdate    { get; }
    public ReactiveCommand<ModItem, Unit> OnInstall   { get; }
    public ReactiveCommand<ModItem, Unit> OnUninstall { get; }
    public ReactiveCommand<ModItem, Unit> OnEnable    { get; }

    [Notify("ProgressBarIndeterminate")] 
    private bool _pbIndeterminate;
    
    [Notify("Progress")] 
    private double _pbProgress;

    [Notify("ProgressBarVisible")] 
    private bool _pbVisible;

    [Notify] 
    private string? _search;

    [Notify]
    private ModItem? _selectedModItem;

    [Notify] 
    private Tag _selectedTag = Tag.All;
    
    [Notify] 
    private Func<ModItem, bool> _searchFilter = ConstTrue;

    [Notify] 
    private Func<ModItem, bool> _selectionFilter = ConstTrue;

    [Notify] 
    private Func<ModItem, bool> _tagFilter = ConstTrue;

    [Notify]
    private bool _updating;

    private readonly ILogger _logger;

    public ModState Api      => _mods.ApiInstall;
    public bool ApiOutOfDate => _mods.ApiInstall is InstalledState { Version: var v } && v.Major < _db.Api.Version;
    public bool CanUpdateAll => _items.Any(x => x.State is InstalledState { Updated: false }) && !_updating;
    public ReadOnlyObservableCollection<ModItem> FilteredItems => _filteredItems;

    public ModPageViewModel(ISettings settings, IModDatabase db, IInstaller inst, IModSource mods, ILogger logger, HttpClient hc)
    {
        _settings = settings;
        _installer = inst;
        _mods = mods;
        _db = db;
        _logger = logger;
        _hc = hc;

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

        ToggleApi = ReactiveCommand.CreateFromTask(ToggleApiCommand);
        UpdateApi = ReactiveCommand.CreateFromTask(UpdateApiAsync);
        ReinstallApi = ReactiveCommand.CreateFromTask(ReinstallApiAsync);
        UpdateAll = ReactiveCommand.CreateFromTask(UpdateAllAsync);

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

    private async Task ReinstallApiAsync()
    {
        _logger.LogInformation("Reinstalling API, {State}", _mods.ApiInstall);
        
        await _installer.InstallApi(IInstaller.ReinstallPolicy.ForceReinstall);
    }

    private async Task ToggleApiCommand()
    {
        _logger.LogInformation("Toggling API, current state: {State}", _mods.ApiInstall);
        
        if (_mods.ApiInstall is not InstalledState)
            await _installer.InstallApi();
        else 
            await _installer.ToggleApi();
        
        RaisePropertyChanged(nameof(Api));
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

    public void SelectAll()       => SelectionFilter = ConstTrue;
    public void SelectInstalled() => SelectionFilter = x => x.Installed;
    public void SelectUnupdated() => SelectionFilter = x => x.State is InstalledState { Updated: false };
    public void SelectEnabled()   => SelectionFilter = x => x.State is InstalledState { Enabled: true };

    public async Task UpdateAllAsync()
    {
        _logger.LogInformation("Updating all mods!");
        
        Updating = true;

        var outOfDate = _items.Where(x => x.State is InstalledState { Updated: false }).ToList();

        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var mod in outOfDate)
        {
            // Mods can get updated as dependencies of others while doing this
            if (mod.State is not InstalledState { Updated: false })
                continue;

            await OnUpdateAsync(mod);
        }

        Updating = false;
    }
    
    [UsedImplicitly]
    private async Task OnEnableAsync(ModItem item)
    {
        try
        {
            if (!item.Enabled || await CheckDependents(item, onlyEnabled: true))
                await _installer.Toggle(item);

            // Reset the visuals of the toggle, as otherwise
            // it remains 'toggled', despite possibly being cancelled by
            // CheckDependents - leading to an incorrectly shown state
            item.CallOnPropertyChanged(nameof(item.Enabled));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error when enabling {Mod}!", item.Name);
            ExceptionRaised?.Invoke(ModAction.Toggle, e, item);
        }
    }

    private async Task UpdateApiAsync()
    {
        try
        {
            await Task.Run(() => _installer.InstallApi());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error when updating API!");
            ExceptionRaised?.Invoke(ModAction.Toggle, e, null);
        }

        RaisePropertyChanged(nameof(ApiOutOfDate));
    }

    private async Task InternalUpdateInstallAsync(ModAction type, ModItem item, Func<IInstaller, Action<ModProgressArgs>, Task> f)
    {
        _logger.LogInformation("Performing {Type} for {Mod}.", type, item);
        
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
        catch (Exception e)
        {
            _logger.LogError(e, "Error when performing {Action} for {Mod}!", type, item.Name);

            ExceptionRaised?.Invoke(type, e, item);
            
            // Even if we threw, stop the progress bar.
            ProgressBarVisible = false;

            // Don't need to sort as we didn't successfully install anything
            // and we don't want to send the successfully completed action event.
            return;
        }

        ProgressBarVisible = false;

        static int Comparer(ModItem x, ModItem y)
        {
            return ModToOrderedTuple(x).CompareTo(ModToOrderedTuple(y));
        }

        CompletedAction?.Invoke(type, item);

        _items.SortBy(Comparer);
        
        _logger.LogInformation("Completed {Type} for {Mod}", type, item.Name);
    }

    [UsedImplicitly]
    private Task OnUpdateAsync(ModItem item) => InternalUpdateInstallAsync(ModAction.Update, item, item.OnUpdate);

    [UsedImplicitly]
    private Task OnInstallAsync(ModItem item) => InternalUpdateInstallAsync(ModAction.Install, item, item.OnInstall);

    private async Task<bool> CheckDependents(ModItem item, bool onlyEnabled = false)
    {
        var deps = onlyEnabled
            ? _reverseDependencySearch.GetAllEnabledDependents(item)
            : _reverseDependencySearch.GetDependents(item);

        if (deps.Count == 0)
            return true;

        return await DisplayHasDependentsWarning(item.Name, deps);
    }
    
    [UsedImplicitly]
    private async Task OnUninstallAsync(ModItem item)
    {
        if (!await CheckDependents(item))
            return;

        await InternalUpdateInstallAsync(ModAction.Uninstall, item, item.OnUninstall);
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

        // Make sure we also don't return true if X was clicked instead of No
        return result.HasFlag(ButtonResult.Yes) && !result.HasFlag(ButtonResult.None);
    }

    private static (int priority, string name) ModToOrderedTuple(ModItem m)
    {
        return (
            m.State is InstalledState { Updated : false } ? -1 : 1,
            m.Name
        );
    }

    public async Task<(string? repo, string content)?> FetchReadme(ModItem item)
    {
        if (_readmes.TryGetValue(item, out var cached))
            return cached;
        
        return await Task.Run(async () =>
        {
            // If we couldn't get it from the raw, we fall back
            // to the actual gh API, as this handles more cases
            // (e.g. non-MD readmes, non-main/master branches, etc.)
            var res = await FetchReadmeRaw(item) ?? await FetchGithubReadme(item);

            if (res is not null)
                _readmes[item] = res;
            
            return res;
        });
    }

    private async Task<(string repo, string content)?> FetchReadmeRaw(ModItem item)
    {
        var raws = new[]
        {
            FetchReadmeRaw(item, branch: "master"),
            FetchReadmeRaw(item, branch: "main"),
        };

        var res = await Task.WhenAll(raws);

        return res.FirstOrDefault(x => x is { } readme && !readme.content.StartsWith("404"));
    }

    private async Task<(string repo, string content)?> FetchReadmeRaw(ModItem item, string branch)
    {
        var uri = new UriBuilder(item.Repository) {
            Host = "raw.githubusercontent.com"
        };

        uri.Path = $"{uri.Path.TrimEnd('/')}/{branch}/";

        var repo = uri.Uri;

        uri.Path += "README.md";
        
        var req = new HttpRequestMessage
        {
            RequestUri = uri.Uri,
            Method = HttpMethod.Get
        };

        var msg = await _hc.SendAsync(req);

        if (msg.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            return null;
            
        return (repo.ToString(), await msg.Content.ReadAsStringAsync());
    }

    private async Task<(string repo, string content)?> FetchGithubReadme(ModItem item)
    {
        var repo = new UriBuilder(item.Repository) {
            Host = "api.github.com"
        };
        repo.Path = $"repos/{repo.Path.TrimEnd('/').TrimStart('/')}/readme";

        var req = new HttpRequestMessage()
        {
            RequestUri = repo.Uri,
            Method = HttpMethod.Get
        };

        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.json"));
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        var msg = await _hc.SendAsync(req);

        // Forbidden means we've hit the rate limit, but in that case the other requests
        // failed, so realistically it's just not there - so we return null.
        if (msg.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            return null;

        if (await msg.Content.ReadFromJsonAsync<JsonElement?>() is not { } elem)
            return null;
        
        var download_url = elem.GetProperty("download_url").GetString() 
                           ?? throw new InvalidDataException("Response is missing download_url!");
        
        var base64 = elem.GetProperty("content").GetString() 
                     ?? throw new InvalidDataException("Response is missing content!");

        return (
            download_url[..(download_url.LastIndexOf('/') + 1)], 
            Encoding.UTF8.GetString(Convert.FromBase64String(base64))
        );
    }
}