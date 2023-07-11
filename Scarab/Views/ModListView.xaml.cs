using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using JetBrains.Annotations;
using ReactiveUI;
using Scarab.Models;
using Scarab.ViewModels;

namespace Scarab.Views;

public partial class ModListView : View<ModListViewModel>
{
    public ModListView()
    {
        InitializeComponent();

        Resources.Add("ToggleIcon", new ToggleIconConverter());

        this.WhenAnyValue(x => ((Control)x).DataContext)
            .BindTo(this, x => x.DataContext);

        this.WhenAnyValue(x => x.TagBox.SelectionBoxItem)
            .Subscribe(x =>
            {
                // It's non-nullable by NRTs, but we initialize it after the constructor, and we can't
                // pass it in earlier as the XAML requires a (public) parameterless constructor
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (DataContext is not null)
                    DataContext.SelectedTag = (Tag)(x ?? Models.Tag.All);
            });

        UserControl.KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!Search.IsFocused)
            Search.Focus();
    }

    
}

public class ToggleIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if ((bool)value)
            return "fa-solid fa-chevron-right";
        return "fa-solid fa-chevron-left";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}