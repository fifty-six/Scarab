using Avalonia.Data.Converters;

namespace Scarab.Converters;

public static class ModStateConverters
{
    public static readonly IValueConverter ToApiToggleInstallString = new FuncValueConverter<ModState, string>(
        s => s switch
        {
            InstalledState { Enabled: true  }  => Resources.MLVM_ApiButtonText_DisableAPI,
            InstalledState { Enabled: false }  => Resources.MLVM_ApiButtonText_EnableAPI,
            NotInstalledState                  => Resources.MI_InstallText_NotInstalled + " API",
            _                                  => throw new ArgumentOutOfRangeException(nameof(s))
        }
    );

    public static readonly IValueConverter IsInstalled = new FuncValueConverter<ModState, bool>(
        s => s is InstalledState
    );
    
    public static readonly IValueConverter IsNotInstalled = new FuncValueConverter<ModState, bool>(
        s => s is InstalledState
    );

    public static readonly IValueConverter IsInstalledAndEnabled = new FuncValueConverter<ModState, bool>(
        s => s is InstalledState { Enabled: true }
    );
}