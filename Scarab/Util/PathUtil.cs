using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;

namespace Scarab.Util
{
    public static class PathUtil
    {
        // There isn't any [return: MaybeNullWhen(param is null)] so this overload will have to do
        // Not really a huge point but it's nice to have the nullable static analysis
        public static async Task<string?> SelectPathFallible() => await SelectPath(true);
        
        public static async Task<string> SelectPath(bool fail = false)
        {
            Debug.WriteLine("Selecting path...");

            Window parent = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                            ?? throw new InvalidOperationException();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return await SelectMacApp(parent, fail);

            while (true)
            {
                IStorageFolder? result = (await parent.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = Resources.PU_SelectPath
                })).FirstOrDefault();
            

                if (result is null)
                    await MessageBoxManager.GetMessageBoxStandardWindow(Resources.PU_InvalidPathTitle, Resources.PU_NoSelect).Show();
                else if (ValidateWithSuffix(result.Path.LocalPath) is not var (managed, suffix))
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
            while (true)
            {
                IStorageFile? result = (await parent.StorageProvider.OpenFilePickerAsync(
                    new FilePickerOpenOptions
                    {
                        AllowMultiple = false,
                        FileTypeFilter = new[] { new FilePickerFileType("app") { Patterns = new[] { "*.app" } } }
                    }
                )).FirstOrDefault();

                if (result is null)
                    await MessageBoxManager.GetMessageBoxStandardWindow(Resources.PU_InvalidPathTitle, Resources.PU_NoSelectMac).Show();
                else if (ValidateWithSuffix(result.Path.AbsolutePath) is not var (managed, suffix))
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