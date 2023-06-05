using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using JetBrains.Annotations;
using Scarab.ViewModels;

namespace Scarab.Views
{
    public partial class ModListView : View<ModListViewModel>
    {
        public ModListView()
        {
            InitializeComponent();

            UserControl.KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (!Search.IsFocused)
                Search.Focus();
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

            if (string.IsNullOrEmpty(txt.Text))
                return;

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
