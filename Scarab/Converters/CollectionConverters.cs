using Avalonia.Data.Converters;

namespace Scarab.Converters;

public static class CollectionConverters
{
    public static readonly IValueConverter ConcatStrLines = new FuncValueConverter<IEnumerable<object>, string>(
        s => string.Join(Environment.NewLine, s?.Select(x => x.ToString()) ?? throw new ArgumentNullException(nameof(s)))
    );
    
    public static readonly IValueConverter ToStrings = new FuncValueConverter<IEnumerable<object>, IEnumerable<string>>(
        c => c?.Select(static x => x.ToString() ?? "null") ?? throw new ArgumentException(nameof(ToStrings))
    );
}