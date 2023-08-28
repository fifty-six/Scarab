using Avalonia.Controls.ApplicationLifetimes;
using MessageBox.Avalonia;

namespace Scarab.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public ISettings Settings { get; }
    private readonly IModSource _mods;

    public static string[] Languages => new[] {
        "en-US",
        "fr",
        "hu-HU",
        "pt-BR",
        "zh"
    };

    public ReactiveCommand<Unit, Unit> ChangePath { get; }

    [Notify]
    private string? _selected;

    public SettingsViewModel(ISettings settings, IModSource mods)
    {
        Settings = settings;
        _mods = mods;
        
        Selected = settings.PreferredCulture;

        ChangePath = ReactiveCommand.CreateFromTask(ChangePathAsync);

        this.WhenAnyValue(x => x.Selected)
            .Subscribe(item =>
            {
                if (string.IsNullOrEmpty(item))
                    return;

                settings.PreferredCulture = item;
                
                settings.Apply();
                settings.Save();
            });
    }
    
    private async Task ChangePathAsync()
    {
        string? path = await PathUtil.SelectPathFallible();

        if (path is null)
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