using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using Scarab.ViewModels;

namespace Scarab.Views
{
    [UsedImplicitly]
    public class ModListView : View<ModListViewModel>
    {
        private readonly TextBox _search;
        private readonly ComboBox _searchOptionsBox;

        public ModListView()
        {
            InitializeComponent();

            this.FindControl<UserControl>(nameof(UserControl)).KeyDown += OnKeyDown;
            
            _search = this.FindControl<TextBox>("Search");

            _searchOptionsBox = this.FindControl<ComboBox>("Search Option Box");

            _searchOptionsBox.SelectionChanged += (_, _) =>
            {
                _searchOptionsBox.SelectedIndex = -1;
                _searchOptionsBox.SelectedItem = null;
            };
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (!_search.IsFocused)
                _search.Focus();
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

        [UsedImplicitly]
        private void RepositoryTextClick(object? sender, PointerReleasedEventArgs _)
        {
            if (sender is not TextBlock txt)
            {
                Trace.TraceWarning($"{nameof(RepositoryTextClick)} called with non TextBlock sender!");
                return;
            }

            Trace.WriteLine(txt.Text);

            try
            {

                Process.Start
                (
                    new ProcessStartInfo(txt.Text)
                    {
                        UseShellExecute = true
                    }
                );
            }
            catch (Exception e)
            {
                Trace.TraceError($"{nameof(RepositoryTextClick)} process spawn failed with error {e}");
            }
        }
    }
}
