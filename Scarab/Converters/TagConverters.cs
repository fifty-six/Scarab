using System.Text;
using Avalonia.Data.Converters;
using Scarab.Models;

namespace Scarab.Converters;

public class TagConverters
{
    public static IValueConverter AsString = new FuncValueConverter<Tag, string>(
        t =>
        {
            var sb = new StringBuilder();
            bool first = true;

            for (int i = 0; i < sizeof(Tag); i++)
            {
                var tag = (Tag) (1 << i);

                if (!t.HasFlag(tag))
                    continue;

                if (!first)
                    sb.Append('\n');
                else
                    first = false;

                sb.Append(tag);
            }

            return sb.ToString();
        }
    );
}