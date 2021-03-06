using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Modinstaller2.ViewModels;

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

        private void OnSearchTextChanged(object sender, EventArgs e)
        {
            var acb = (AutoCompleteBox) sender;
            var model = (ModListViewModel) acb.DataContext;
            
            model.FilterItems(acb.Text);
        }
    }
}
