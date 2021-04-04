using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MessageBox.Avalonia;

namespace Modinstaller2.Util
{
    public static class SelectPathUtil
    {
        public static async Task<string> SelectPath()
        {
            Debug.WriteLine("Selecting path...");

            Window parent = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return await SelectMacApp(parent);
            }

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

                    continue;
                }

                if (IsValid(result))
                    continue;

                await MessageBoxManager.GetMessageBoxStandardWindow
                (
                    "Path",
                    "Invalid Hollow Knight Path. Select the Hollow Knight folder containing hollow_knight_Data."
                )
                .Show();

                result = null;
            }
            while (result == null);

            return result;
        }

        private static async Task<string> SelectMacApp(Window parent)
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

            return result.First();
        }

        private static bool IsValid(string result)
        {
            return Directory.Exists(result)
                && Directory.Exists(Path.Combine(result, Settings.OSManagedSuffix))
                && File.Exists(Path.Combine(result, Settings.OSManagedSuffix, "Assembly-CSharp.dll"));
        }
    }
}