using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using Scarab.Views;

namespace Scarab.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public ISettings Settings { get; }
    
    private readonly IModSource _mods;

    public static string[] Languages => new[]
    {
        "en-US",
        "fr",
        "hu-HU",
        "pt-BR",
        "zh"
    };

    public static ThemeVariant[] Themes => new[]
    {
        ThemeVariant.Dark,
        ThemeVariant.Light
    };

    public ReactiveCommand<Unit, Unit> ChangePath { get; }

    [Notify] 
    private ThemeVariant _theme;

    [Notify] 
    private string? _selected;

    public SettingsViewModel(ISettings settings, IModSource mods)
    {
        Settings = settings;
        _mods = mods;

        Selected = settings.PreferredCulture;

        ChangePath = ReactiveCommand.CreateFromTask(ChangePathAsync);

        _theme = settings.PreferredTheme == Models.Theme.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        this.ObservableForProperty(x => x.Selected)
            .Subscribe(o =>
            {
                var item = o.GetValue();
                
                if (string.IsNullOrEmpty(item))
                    return;

                settings.PreferredCulture = item;
                
                settings.Apply();
                settings.Save();
            });

        this.ObservableForProperty(x => x.Theme)
            .Subscribe(
                o =>
                {
                    var t = o.GetValue();
                    
                    Application.Current!.RequestedThemeVariant = t;
                    settings.PreferredTheme = t == ThemeVariant.Dark
                        ? Models.Theme.Dark
                        : Models.Theme.Light;

                    settings.Save();
                }
            );
    }

    private async Task ChangePathAsync()
    {
        var main = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                   ?? throw new InvalidOperationException();
        
        var res = await PathUtil.TrySelection(main);

        string? path;

        if (res is not ValidPath (var root, var suffix))
        {
            var w = new PathWindow { ViewModel = new PathViewModel(res) };
            
            path = await w.ShowDialog<string?>(main);
        }
        else
        {
            path = Path.Combine(root, suffix);
        }

        // User closed out of the dialog
        if (string.IsNullOrEmpty(path))
            return;
        
        Settings.ManagedFolder = path;
        Settings.Save();

        await _mods.Reset();

        await MessageBoxManager.GetMessageBoxStandardWindow(Resources.MLVM_ChangePathAsync_Msgbox_Title,
            Resources.MLVM_ChangePathAsync_Msgbox_Text).Show();

        // Shutting down is easier than re-doing the source and all the items.
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}