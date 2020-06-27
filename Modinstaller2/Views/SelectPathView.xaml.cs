using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;

namespace Modinstaller2.Views
{
    [UsedImplicitly]
    public class SelectPathView : UserControl
    {
        public SelectPathView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
