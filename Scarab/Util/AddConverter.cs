using System.Linq;
using Avalonia.Data.Converters;

namespace Scarab.Util;

public static class AddConverter
{
    public static readonly IMultiValueConverter Instance = new FuncMultiValueConverter<double, double>(x => x.Sum());
}