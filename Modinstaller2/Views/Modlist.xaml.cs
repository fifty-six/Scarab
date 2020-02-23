using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Modinstaller2.Views
{
    public class Modlist : UserControl
    {
        public Modlist()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
