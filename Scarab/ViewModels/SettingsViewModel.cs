using System;
using JetBrains.Annotations;
using PropertyChanged.SourceGenerator;
using ReactiveUI;
using Scarab.Interfaces;

namespace Scarab.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [UsedImplicitly]
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

                settings.PreferredCulture = item;
                
                settings.Apply();
                settings.Save();
            });
    }
}