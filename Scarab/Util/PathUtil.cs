using System.Runtime.InteropServices;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Scarab.Views;

namespace Scarab.Util;

public static class PathUtil
{
    public static async Task<string> SelectPath(Window? parent = null)
    {
        Log.Information("Selecting path...");

        parent ??= (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
                   ?? throw new InvalidOperationException();

        PathResult res = await TrySelection(parent);

        while (true)
        {
            if (res is not ValidPath (var managed, var suffix))
            {
                Log.Information("Invalid path selection! {Result}", res);
                
                var w = new PathWindow { ViewModel = new PathViewModel(res) };

                // The dialog asks the user to select again, so we check
                // if we got a non-null path back from it
                if (await w.ShowDialog<string?>(parent) is { } p)
                    return p;
            }
            else
            {
                return Path.Combine(managed, suffix);
            }
        }
    }

    public static async Task<PathResult> TrySelection(Window? parent = null)
    {
        string LocalizeToOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return string.Format(Resources.PU_SelectPath, "app");
            
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return string.Format(Resources.PU_SelectPath, "exe");
            
            // Default to the linux one, 
            return string.Format(Resources.PU_SelectPath, "hollow_knight.x86_64");
        }
        
        parent ??= Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime { MainWindow: { } main }
            ? main
            : throw new InvalidOperationException("No window found!");
        
        IStorageFile? result = (await parent.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = LocalizeToOS(),
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Hollow Knight file") {
                    Patterns = new[] { "*.app", "*.exe", "*.x86_64", "Hollow Knight" }
                } }
            }
        )).FirstOrDefault();

        if (result is not { Path.LocalPath: var localPath })
            return new PathNotSelectedError();

        var path = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? localPath
            : Path.GetDirectoryName(localPath)!;

        return ValidateWithSuffix(path);
    }
    
    internal static readonly string[] SUFFIXES =
    {
        // GoG
        "Hollow Knight_Data/Managed",
        // Steam
        "hollow_knight_Data/Managed",
        // Mac
        "Contents/Resources/Data/Managed"
    };

    public static PathResult ValidateWithSuffix(string? root)
    {
        if (root is null)
            return new PathNotSelectedError();
        
        if (!Directory.Exists(root))
            return new RootNotFoundError();
            
        string? suffix = SUFFIXES.FirstOrDefault(s =>
        {
            string p = Path.Combine(root, s);
            
            Log.Information("Trying path {Path}", p);
            
            return Directory.Exists(p);
        });

        if (suffix is null)
        {
            Log.Information("Selected path root {Root} had no valid suffix with Managed folder!", root);

            return new SuffixNotFoundError(root, SUFFIXES.Select(s => Path.Combine(root, s)).ToArray());
        }

        if (File.Exists(Path.Combine(root, suffix, "Assembly-CSharp.dll")))
        {
            Log.Information("Found valid path {Root} / {Suffix}", root, suffix);
            return new ValidPath(root, suffix);
        }

        Log.Information(
            "Selected path root {Path} with suffix {Suffix} was missing Assembly-CSharp.dll!",
            root,
            suffix
        );
            
        return new AssemblyNotFoundError(
            Path.Combine(root, suffix),
            new[] { Path.Combine(root, suffix, "Assembly-CSharp.dll") }
        );

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

    public static string BasePath(string managed)
    {
        return SUFFIXES.Select(s => managed.EndsWith(s) ? managed[..^s.Length] : null)
                       .FirstOrDefault(x => x is not null) 
               ?? managed;
    }
}