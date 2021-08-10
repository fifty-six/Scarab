using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using Modinstaller2.ViewModels;

namespace Modinstaller2.Views
{
    [UsedImplicitly]
    public class ModListView : View<ModListViewModel>
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
}
