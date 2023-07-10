using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scarab.ViewModels;

namespace Scarab.Views;

public partial class SettingsView : View<SettingsViewModel>
{
    public SettingsView()
    {
        InitializeComponent();
    }
}

public class SettingsViewModel : ViewModelBase
{
}