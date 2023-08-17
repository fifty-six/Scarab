using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Scarab.ViewModels;

namespace Scarab.Views;

public partial class ModListView : ReactiveUserControl<ModPageViewModel>
{
    public ModListView()
    {
        InitializeComponent();
    }
}