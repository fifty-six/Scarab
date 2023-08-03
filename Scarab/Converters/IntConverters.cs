using Avalonia.Data.Converters;

namespace Scarab.Converters;

public static class IntConverters
{
    public static readonly IValueConverter NonZero = new FuncValueConverter<int, bool>(x => x != 0);
}