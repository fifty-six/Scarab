using System;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Scarab.Models;
using Scarab.ViewModels;

namespace Scarab.Views;

public partial class ModPageView : ReactiveUserControl<ModPageViewModel>
{
    public ModPageView()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.DataContext)
            .BindTo(this, x => x.ViewModel);

        this.WhenAnyValue(x => x.TagBox.SelectionBoxItem)
            .Subscribe(x =>
            {
                // It's non-nullable by NRTs, but we initialize it after the constructor, and we can't
                // pass it in earlier as the XAML requires a (public) parameterless constructor
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (ViewModel is not null)
                    ViewModel.SelectedTag = (Tag)(x ?? Models.Tag.All);
            });

        UserControl.KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!Search.IsFocused)
            Search.Focus();
    }

    
}