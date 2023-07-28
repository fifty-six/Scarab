using System;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Scarab.ViewModels;

namespace Scarab.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        // Need to wait for the data context to be initialized,
        // as it's set shortly *after* the constructor.
        DataContextChanged += OnDataContextSet;
    }

    private void OnDataContextSet(object? _, EventArgs _e)
    {
        if (DataContext is null)
            return;

        var vm = (MainWindowViewModel)DataContext;

        vm.WhenAnyValue(x => x.Content).Subscribe(v =>
        {
            if (v is not null)
                ModListTab.Content = v;
        });
    }
}