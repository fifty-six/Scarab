using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using JetBrains.Annotations;
using ReactiveUI;
using Scarab.Models;
using Scarab.ViewModels;
// Resources is a field in Avalonia UserControls, so alias it for brevity
using Localization = Scarab.Resources;

namespace Scarab.Views;

[UsedImplicitly]
public partial class ModPageView : ReactiveUserControl<ModPageViewModel>
{
    private WindowNotificationManager? _notify;

    public ModPageView()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.DataContext)
            .BindTo(this, x => x.ViewModel);

        this.WhenAnyValue(x => x.ViewModel)
            .Subscribe(vm =>
            {
                if (vm is not null) 
                    vm.CompletedAction += OnComplete;
            });

        this.WhenAnyValue(x => x.TagBox.SelectionBoxItem)
            .Subscribe(x =>
            {
                // It's non-nullable by NRTs, but we initialize it after the constructor, and we can't
                // pass it in earlier as the XAML requires a (public) parameterless constructor
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (ViewModel is not null)
                    ViewModel.SelectedTag = (Tag) (x ?? Models.Tag.All);
            });

        UserControl.KeyDown += OnKeyDown;
    }

    private void OnComplete(ModPageViewModel.ModAction act, ModItem mod)
    {
        string act_s = act switch
        {
            ModPageViewModel.ModAction.Install => Localization.NOTIFY_Installed,
            ModPageViewModel.ModAction.Update => Localization.NOTIFY_Updated,
            ModPageViewModel.ModAction.Uninstall => Localization.NOTIFY_Uninstalled,
            _ => throw new ArgumentOutOfRangeException(nameof(act), act, null)
        };
        
        _notify?.Show(new Notification(
            "Success!",
            $"{act_s} {mod.Name}!",
            NotificationType.Success
        ));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var tl = TopLevel.GetTopLevel(this);
        
        _notify = new WindowNotificationManager(tl) { MaxItems = 3 };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!Search.IsFocused)
            Search.Focus();
    }

    
}