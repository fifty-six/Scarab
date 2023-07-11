using JetBrains.Annotations;
using Scarab.ViewModels;

namespace Scarab.Views;

[UsedImplicitly]
public partial class AboutView : View<AboutViewModel>
{
    public AboutView()
    {
        InitializeComponent();
    }
}