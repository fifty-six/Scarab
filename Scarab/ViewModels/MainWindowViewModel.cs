using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using System.Net.Http.Headers;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;

namespace Scarab.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly ReactiveCommand<Unit, Unit> _initialization;
    
    private static bool _Debug
    {
        get {
#if DEBUG
            return true;
#else
                return false;
#endif
        }
    }
        
    [Notify]
    private string? _infoText;

    [Notify]
    private ReactiveObject? _content;

    [Notify] 
    private SettingsViewModel? _settingsPage;

    private async Task Impl()
    {
        Log.Information("Checking if up to date...");
            
        var con = new Container();

        Log.Information("Loading settings.");
        var settings = Settings.Load() ?? Settings.Create(await GetSettingsPath());
        settings.Apply();
        
        await CheckUpToDate(settings);

        if (!PathUtil.ValidateExisting(settings.ManagedFolder))
        {
            Log.Information("Settings path {Previous} is invalid, forcing re-selection.", settings.ManagedFolder);
            settings = await ResetSettings();
        }

        Log.Information("Fetching links");
            
        (ModLinks ml, ApiLinks al) content;

        void AddSettings(HttpClient hc)
        {
            hc.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                MustRevalidate = true
            };
                
            hc.DefaultRequestHeaders.Add("User-Agent", "Scarab");
        }

        HttpClient hc;
            
        try
        {
            var res = await WorkaroundHttpClient.TryWithWorkaroundAsync(
                settings.RequiresWorkaroundClient 
                    ? WorkaroundHttpClient.Settings.OnlyWorkaround
                    : WorkaroundHttpClient.Settings.TryBoth,
                ModDatabase.FetchContent,
                AddSettings
            );

            content = res.Result;

            if (res.NeededWorkaround && !settings.RequiresWorkaroundClient)
            {
                settings.RequiresWorkaroundClient = true;
                settings.Save();
            }

            hc = res.Client;
        }
        catch (Exception e) when (e is TaskCanceledException { CancellationToken.IsCancellationRequested: true } or HttpRequestException)
        {
            var failedOp = e switch
            {
                TaskCanceledException => Resources.MWVM_Impl_Error_Fetch_ModLinks_Timeout,
                HttpRequestException http => string.Format(Resources.MWVM_Impl_Error_Fetch_ModLinks_Error, http.StatusCode),
                _ => throw new ArgumentOutOfRangeException()
            };
                
            await MessageBoxManager.GetMessageBoxStandard
            (
                title: Resources.MWVM_Impl_Error_Fetch_ModLinks_Msgbox_Title,
                text: string.Format(Resources.MWVM_Impl_Error_Fetch_ModLinks_Msgbox_Text, failedOp),
                icon: Icon.Error
            ).ShowAsync();

            throw;
        }

        Log.Information("Fetched links successfully");

        con.AddLogging();
        con.RegisterInstance(hc);
        con.RegisterDelegate<ISettings>(_ => settings);
        con.Register<IFileSystem, FileSystem>();
        
        var mods = await InstalledMods.Load(
            con.GetRequiredService<IFileSystem>(),
            settings,
            content.ml
        );

        con.RegisterInstance<IModSource>(mods);
        con.RegisterDelegate<IModSource, IModDatabase>(src => new ModDatabase(src, content));
        con.Register<IInstaller, Installer>();
        con.Register<ModPageViewModel>();
        con.Register<SettingsViewModel>();

        con.ValidateAndThrow();
        
        Logger.Sink = new MicrosoftLogSink(
            con.Resolve<ILoggerFactory>().CreateLogger("Avalonia"),
            LogArea.Platform,
            LogArea.macOSPlatform,
            LogArea.X11Platform,
            LogArea.Binding,
            LogArea.Animations,
            LogArea.Control,
            LogArea.Property,
            LogArea.Visual
        );
        
        Log.Information("Displaying model");

        if (settings.PlatformChanged)
        {
            var platText = settings.Platform == Settings.GamePlatform.Windows
                ? "Proton"
                : Resources.MWVM_Platform_Native;
            
            InfoText = string.Format(Resources.MWVM_PlatformChanged, platText);
            
            await con.GetRequiredService<IInstaller>().HandlePlatformChange();
        }

        SettingsPage = con.GetRequiredService<SettingsViewModel>();
        Content = con.GetRequiredService<ModPageViewModel>();
    }

    private static async Task CheckUpToDate(Settings settings)
    {
        var current_version = Assembly.GetExecutingAssembly().GetName().Version;
            
        Log.Information("Current version of installer is {Version}", current_version);

        if (_Debug) 
            return;

        const string gh_releases = "https://api.github.com/repos/fifty-six/Scarab/releases/latest";

        string json;
            
        try
        {
            var hc = new HttpClient();
                
            hc.DefaultRequestHeaders.Add("User-Agent", "Scarab");

            json = await hc.GetStringAsync(new Uri(gh_releases));
        }
        catch (Exception e) when (e is HttpRequestException or TimeoutException) {
            return;
        }

        var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("tag_name", out var tag_elem))
            return;

        var body = string.Empty;
        if (doc.RootElement.TryGetProperty("body", out var body_elem))
        {
            body = body_elem.GetString() ?? string.Empty;
            body = string.Join('\n', body.Split('\n')[1..]).Trim();
        }

        var tag = tag_elem.GetString();

        if (tag is null)
            return;

        if (tag.StartsWith("v"))
            tag = tag[1..];

        if (!Version.TryParse(tag.Length == 1 ? tag + ".0.0.0" : tag, out var version))
            return;
        
        if (version <= current_version)
            return;

        if (version <= settings.IgnoredVersion)
        {
            Log.Logger.Information("Skipping version {Version}", version);
            return;
        }

        var res = await MessageBoxManager.GetMessageBoxCustom
        (
            new MessageBoxCustomParams {
                ButtonDefinitions = new[] {
                    new ButtonDefinition {
                        IsDefault = true,
                        IsCancel = true,
                        Name = Resources.MWVM_OutOfDate_GetLatest
                    },
                    new ButtonDefinition {
                        Name = Resources.MWVM_OutOfDate_ContinueAnyways
                    },
                    new ButtonDefinition {
                        Name = Resources.MWVM_OutOfDate_SkipVersion
                    }
                },
                ContentTitle = Resources.MWVM_OutOfDate_Title,
                ContentHeader = "A new version of Scarab is available!",
                ContentMessage = body,
                SizeToContent = SizeToContent.WidthAndHeight
            }
        ).ShowAsync();

        if (res == Resources.MWVM_OutOfDate_GetLatest)
        {
            Process.Start(new ProcessStartInfo("https://github.com/fifty-six/Scarab/releases/latest") { UseShellExecute = true });
                
            ((IClassicDesktopStyleApplicationLifetime?) Application.Current?.ApplicationLifetime)?.Shutdown();
        }
        else if (res == Resources.MWVM_OutOfDate_SkipVersion)
        {
            Log.Information("Ignoring update {Version}", version);
            settings.IgnoredVersion = version;
            settings.Save();
        }
        else
        {
            Log.Warning(
                "Installer out of date! Version {Current_version} with latest {Version}!",
                current_version,
                version
            );
        }
    }
        
    private static async Task<Settings> ResetSettings()
    {
        await MessageBoxManager.GetMessageBoxStandard
        (
            new MessageBoxStandardParams {
                ContentHeader = Resources.MWVM_Warning,
                ContentMessage = Resources.MWVM_InvalidSavedPath_Message,
                // The auto-resize for this lib is buggy, so 
                // ensure that the message doesn't get cut off 
                MinWidth = 550
            }
        ).ShowAsync();

        return Settings.Create(await GetSettingsPath());
    }

    private static async Task<string> GetSettingsPath()
    {
        if (!Settings.TryAutoDetect(out var path))
        {
            Log.Information("Unable to detect installation path for settings, selecting manually.");
            
            var info = MessageBoxManager.GetMessageBoxStandard
            (
                new MessageBoxStandardParams
                {
                    ContentHeader = Resources.MWVM_Info,
                    ContentMessage = Resources.MWVM_UnableToDetect_Message,
                    MinWidth = 550
                }
            );

            await info.ShowAsync();
                
            return await PathUtil.SelectPath();
        }

        Log.Information("Settings doesn't exist. Creating it at detected path {Path}.", path);

        var window = MessageBoxManager.GetMessageBoxStandard
        (
            new MessageBoxStandardParams
            {
                ContentHeader = Resources.MWVM_DetectedPath_Title,
                ContentMessage = string.Format(Resources.MWVM_DetectedPath_Message, path.Root),
                ButtonDefinitions = ButtonEnum.YesNo
            }
        );

        var res = await window.ShowAsync();

        return res == ButtonResult.Yes
            ? Path.Combine(path.Root, path.Suffix)
            : await PathUtil.SelectPath();
    }

    public MainWindowViewModel()
    {
        _initialization = ReactiveCommand.CreateFromTask(OnInitialized);

        this.WhenActivated(
            disposable => _initialization.Execute().Subscribe().DisposeWith(disposable)
        );
    }

    private async Task OnInitialized()
    {
        try
        {
            await Impl();
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Fatal error in MainWindowViewModel startup!");

            if (Debugger.IsAttached)
                Debugger.Break();

            Environment.Exit(-1);

            throw;
        }
    }
}