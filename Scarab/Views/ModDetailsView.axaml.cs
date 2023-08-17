using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using JetBrains.Annotations;
using Scarab.ViewModels;
using Serilog;

namespace Scarab.Views;

public partial class ModDetailsView : ReactiveUserControl<ModPageViewModel>
{
    public ModDetailsView()
    {
        InitializeComponent();
    }

    [UsedImplicitly]
    private void RepositoryTextClick(object? sender, PointerReleasedEventArgs _)
    {
        if (sender is not TextBlock txt)
        {
            Log.Warning(
                $"{nameof(RepositoryTextClick)} called with non TextBlock sender {{SenderType}}!",
                sender?.GetType().Name ?? "null"
            );
            return;
        }

        if (string.IsNullOrEmpty(txt.Text))
            return;

        try
        {
            Process.Start
            (
                new ProcessStartInfo(txt.Text)
                {
                    UseShellExecute = true
                }
            );
        }
        catch (Exception e)
        {
            Log.Error(e, $"{nameof(RepositoryTextClick)} process spawn failed!`");
        }
    }
}