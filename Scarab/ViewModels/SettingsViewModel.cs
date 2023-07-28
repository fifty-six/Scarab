using System;
using System.Globalization;
using PropertyChanged.SourceGenerator;
using ReactiveUI;
using Scarab.Extensions;
using Scarab.Interfaces;

namespace Scarab.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private ISettings Settings { get; }

    public static string[] Languages => new[] {
        "en-US",
        "fr",
        "hu-HU",
        "pt-BR",
        "zh"
    };

    [Notify]
    private string? _selected;

    public SettingsViewModel(ISettings settings)
    {
        Settings = settings;
        Selected = settings.PreferredCulture;

        this.WhenAnyValue(x => x.Selected)
            .Subscribe(item =>
            {
                if (string.IsNullOrEmpty(item))
                    return;

                LocalizeExtension.ChangeLanguage(new CultureInfo(item));
                
                settings.PreferredCulture = item;
                
                settings.Save();
            });
    }
}