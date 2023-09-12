using Avalonia.Data.Converters;

namespace Scarab.Converters;

public static class ISettingsConverters
{
    public static readonly IValueConverter ToBasePath =
        new FuncValueConverter<ISettings, string>(s =>
        {
            ArgumentNullException.ThrowIfNull(s);
            return PathUtil.BasePath(s.ManagedFolder);
        } 
    );
}