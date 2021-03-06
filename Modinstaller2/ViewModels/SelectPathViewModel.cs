using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using JetBrains.Annotations;
using MessageBox.Avalonia;
using ReactiveUI;
using SPath = System.IO.Path;

namespace Modinstaller2.ViewModels
{
    public class SelectPathViewModel : ViewModelBase
    {
        private string _path;

        internal string Path
        {
            get => _path;
            private set => this.RaiseAndSetIfChanged(ref _path, value);
        }

        [UsedImplicitly]
        public ReactiveCommand<Unit, Unit> SelectCommand { get; }

        public SelectPathViewModel()
        {
            SelectCommand = ReactiveCommand.CreateFromTask(Select);
        }

        private async Task Select()
        {
            Debug.WriteLine("Selecting path...");

            Window parent = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select your Hollow Knight app.",
                    AllowMultiple = false
                };

                dialog.Filters.Add(new FileDialogFilter { Extensions = { "app" } });

                string[] result;

                do
                {
                    result = await dialog.ShowAsync(parent);

                    if (result == null || result.Length == 0)
                    {
                        result = null;
                        
                        await MessageBoxManager.GetMessageBoxStandardWindow("Path", "Please select your Hollow Knight App.").Show();
                    }
                    else if (!IsValid(result.First()))
                    {
                        result = null;

                        await MessageBoxManager.GetMessageBoxStandardWindow
                                               (
                                                   "Path",
                                                   "Invalid Hollow Knight App. Assembly-CSharp or Managed folder missing."
                                               )
                                               .Show();
                    }
                }
                while (result == null);

                Path = result.First();
            }
            else
            {
                string result;

                var dialog = new OpenFolderDialog
                {
                    Title = "Select your Hollow Knight folder."
                };

                do
                {
                    result = await dialog.ShowAsync(parent);

                    if (result == null)
                    {
                        await MessageBoxManager.GetMessageBoxStandardWindow("Path", "Please select your Hollow Knight Path.").Show();
                    }
                    else if (!IsValid(result))
                    {
                        result = null;

                        await MessageBoxManager.GetMessageBoxStandardWindow
                                               (
                                                   "Path",
                                                   "Invalid Hollow Knight Path. Select the Hollow Knight folder containing hollow_knight_Data."
                                               )
                                               .Show();
                    }
                }
                while (result == null);

                Path = result;
            }
        }

        private static bool IsValid(string result)
        {
            return Directory.Exists(result)
                && Directory.Exists(SPath.Combine(result, InstallerSettings.OSManagedSuffix))
                && File.Exists(SPath.Combine(result, InstallerSettings.OSManagedSuffix, "Assembly-CSharp.dll"));
        }
    }
}