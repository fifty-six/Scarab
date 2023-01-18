using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;

namespace Scarab.Util
{
    public static class PathUtil
    {
        // There isn't any [return: MaybeNullWhen(param is null)] so this overload will have to do
        // Not really a huge point but it's nice to have the nullable static analysis
        public static async Task<string?> SelectPathFailable() => await SelectPath(true);
        
        public static async Task<string> SelectPath(bool fail = false)
        {
            Debug.WriteLine("Selecting path...");

            Window parent = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                ?? throw new InvalidOperationException();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return await SelectMacApp(parent, fail);

            var dialog = new OpenFolderDialog
            {
                Title = Resources.PU_SelectPath,
            };

            while (true)
            {
                string? result = await dialog.ShowAsync(parent);

                if (result is null)
                    await MessageBoxManager.GetMessageBoxStandardWindow(Resources.PU_InvalidPathTitle, Resources.PU_NoSelect).Show();
                else if (ValidateWithSuffix(result) is not (var managed, var suffix))
                    await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams {
                        ContentTitle = Resources.PU_InvalidPathTitle,
                        ContentHeader = Resources.PU_InvalidPathHeader,
                        ContentMessage = Resources.PU_InvalidPath,
                        MinHeight = 140
                    }).Show();
                else
                    return Path.Combine(managed, suffix);

                if (fail)
                    return null!;
            }
        }

        private static async Task<string> SelectMacApp(Window parent, bool fail)
        {
            var dialog = new OpenFileDialog
            {
                Title = Resources.PU_SelectApp,
                Directory = "/Applications",
                AllowMultiple = false
            };

            dialog.Filters.Add(new FileDialogFilter { Extensions = { "app" } });

            while (true)
            {
                string[]? result = await dialog.ShowAsync(parent);

                if (result is null or { Length: 0 })
                    await MessageBoxManager.GetMessageBoxStandardWindow(Resources.PU_InvalidPathTitle, Resources.PU_NoSelectMac).Show();
                else if (ValidateWithSuffix(result.First()) is not (var managed, var suffix))
                    await MessageBoxManager.GetMessageBoxStandardWindow(new MessageBoxStandardParams {
                        ContentTitle = Resources.PU_InvalidPathTitle,
                        ContentHeader = Resources.PU_InvalidAppHeader,
                        ContentMessage = Resources.PU_InvalidApp,
                        MinHeight = 200
                    }).Show();
                else
                    return Path.Combine(managed, suffix);

                if (fail)
                    return null!;
            }
        }

        private static readonly string[] SUFFIXES =
        {
            // GoG
            "Hollow Knight_Data/Managed",
            // Steam
            "hollow_knight_Data/Managed",
            // Mac
            "Contents/Resources/Data/Managed"
        };

        public static ValidPath? ValidateWithSuffix(string root)
        {
            if (!Directory.Exists(root))
                return null;
            
            string? suffix = SUFFIXES.FirstOrDefault(s => Directory.Exists(Path.Combine(root, s)));

            if (suffix is null || !File.Exists(Path.Combine(root, suffix, "Assembly-CSharp.dll")))
                return null;

            return new ValidPath(root, suffix);
        }

        public static bool ValidateExisting(string managed)
        {
            // We have the extra case of UnityEngine's dll here
            // because in cases with old directories or previous issues
            // the assembly dll can still exist, but UnityEngine.dll
            // is always unmodified, so we can rely on it.
            return Directory.Exists(managed)
                && File.Exists(Path.Combine(managed, "Assembly-CSharp.dll"))
                && File.Exists(Path.Combine(managed, "UnityEngine.dll"));
        }
    }
}
