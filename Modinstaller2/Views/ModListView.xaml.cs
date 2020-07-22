using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Modinstaller2.Views
{
    public class ModListView : UserControl
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
