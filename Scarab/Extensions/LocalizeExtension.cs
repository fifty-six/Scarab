using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scarab.Extensions
{
    internal class LocalizeExtension : MarkupExtension
    {
        public LocalizeExtension(string key)
        {
            Key = key;
        }
        public string Key { get; set; } = "";
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Resources.ResourceManager.GetString(Key, Resources.Culture)?.Replace("\\n", "\n") ?? $"#{Key}#";
        }
    }
}
