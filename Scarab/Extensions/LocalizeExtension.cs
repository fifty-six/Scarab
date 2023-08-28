using Avalonia.Markup.Xaml;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Data;

namespace Scarab.Extensions;

internal sealed class LocalizeExtension : MarkupExtension, INotifyPropertyChanged
{
    public static void ChangeLanguage(CultureInfo ci)
    {
        Resources.Culture = ci;
        OnLanguageChanged?.Invoke();
    }
        
    private static event Action? OnLanguageChanged;
        
    private string Key { get; }

    public string Result => 
        Resources.ResourceManager.GetString(Key, Resources.Culture)?.Replace("\\n", "\n") ?? $"#{Key}#";

    public LocalizeExtension(string key)
    {
        Key = key;
            
        OnLanguageChanged += () => {
            OnPropertyChanged(nameof(Result));
        };
    }
        
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding(nameof(Result)) { Source = this };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) 
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}