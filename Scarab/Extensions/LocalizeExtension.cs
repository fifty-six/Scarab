using Avalonia.Markup.Xaml;
using System;

namespace Scarab.Extensions
{
    internal class LocalizeExtension : MarkupExtension
    {
        private string Key { get; }
        
        public LocalizeExtension(string key) => Key = key;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Resources.ResourceManager.GetString(Key, Resources.Culture)?.Replace("\\n", "\n") ?? $"#{Key}#";
        }
    }
}
