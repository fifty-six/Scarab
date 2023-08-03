using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Scarab.Models;

namespace Scarab.Views;

[PseudoClasses(":installed", ":installing", ":enabled", ":disabled", ":updated")]
public partial class ModListItem : ReactiveUserControl<ModItem>
{
    public ModListItem()
    {
        InitializeComponent();
    }

    protected override void OnInitialized()
    {
        Debug.Assert(ViewModel is not null);
        
        ViewModel.WhenAnyValue(x => x.State).Subscribe(OnStateChange);
    }

    private void OnStateChange(ModState state)
    {
        switch (state)
        {
            case InstalledState { Enabled: var enabled, Updated: var updated }:
                PseudoClasses.Set(":installed", true);
                PseudoClasses.Set(":enabled", enabled);
                PseudoClasses.Set(":updated", updated);
                break;
            case NotInstalledState { Installing: var installing }:
                PseudoClasses.Set(":installed", false);
                PseudoClasses.Set(":enabled", false);
                PseudoClasses.Set(":updated", false);

                PseudoClasses.Set(":installing", installing);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}