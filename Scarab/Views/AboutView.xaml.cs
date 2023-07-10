using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Scarab.ViewModels;

namespace Scarab.Views;

public partial class AboutView : View<AboutViewModel>
{
    public AboutView()
    {
        InitializeComponent();
    }
}

public class AboutViewModel : ViewModelBase
{
}