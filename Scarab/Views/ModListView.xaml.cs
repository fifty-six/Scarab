using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using Scarab.ViewModels;

namespace Scarab.Views
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
        
        [UsedImplicitly]
        private void PrepareElement(object? sender, ItemsRepeaterElementClearingEventArgs e)
        {
            e.Element.VisualChildren.OfType<Expander>().First().IsExpanded = false;
        }
    }
}
