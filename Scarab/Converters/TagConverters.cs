using System.Collections.Generic;
using Avalonia.Data.Converters;
using Scarab.Models;

namespace Scarab.Converters;

public static class TagConverters
{
    public static IValueConverter NonZero = new FuncValueConverter<Tag, bool>(t => (int) t != 0);
    
    public static IValueConverter AsStrings = new FuncValueConverter<Tag, IEnumerable<string>>(
        t =>
        {
            var l = new List<string>();

            for (int i = 0; i < sizeof(Tag) * 8; i++)
            {
                var tag = (Tag) (1 << i);

                if (!t.HasFlag(tag))
                    continue;

                l.Add(tag.ToString());
            }

            return l;
        }
    );
}