using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Modinstaller2.ViewModels
{
    public class SelectPathViewModel : ViewModelBase 
    {
        internal string _path;

        internal string Path 
        { 
            get => _path;
            set
            {
                this.RaiseAndSetIfChanged(ref _path, value);
            }
        }

        public ReactiveCommand<Unit, Unit> SelectCommand { get; }

        public SelectPathViewModel()
        {
            SelectCommand = ReactiveCommand.CreateFromTask(Select);
        }

        private async Task Select()
        {
            System.Diagnostics.Debug.WriteLine("Selecting path...");

            Window parent = (Avalonia.Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select your Hollow Knight app.",
                };

                dialog.AllowMultiple = false;

                dialog.Filters.Add(new FileDialogFilter() { Extensions = { ".app" } });

                var result = await dialog.ShowAsync(parent);

                // Same todo as below lmao
                // Also note, .app is *technically* a folder, should this be actually using OpenFolderDialog for both?

                System.Diagnostics.Debug.WriteLine($"Got .app path: {result}");

                Path = result.First();
            }
            else 
            {
                var dialog = new OpenFolderDialog()
                {
                    Title = "Select your Hollow Knight folder."
                };

                var result = await dialog.ShowAsync(parent);

#warning TODO: Validation of paths and confirm for result.

                if (result == null)
                {
                    // TODO: Handle this.
                    // Might just loop.
                }

                // Should also do validation.

                System.Diagnostics.Debug.WriteLine($"Got path: {result}");

                Path = result;
            }
        }
    }
}
