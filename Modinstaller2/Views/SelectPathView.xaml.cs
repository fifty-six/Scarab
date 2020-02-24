using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;

namespace Modinstaller2.Views
{
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
