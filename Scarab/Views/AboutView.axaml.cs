using Avalonia.ReactiveUI;
using JetBrains.Annotations;
using Scarab.ViewModels;

namespace Scarab.Views;

[UsedImplicitly]
public partial class AboutView : ReactiveUserControl<AboutViewModel>
{
    public AboutView()
    {
        InitializeComponent();
    }
}