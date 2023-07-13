using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scarab.ViewModels;

namespace Scarab.Views;

public partial class ModListView : View<ModPageViewModel>
{
    public ModListView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}