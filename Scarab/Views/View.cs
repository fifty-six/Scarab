using Avalonia.Controls;

namespace Scarab.Views
{
    public class View<T> : UserControl where T : class
    {
        public new T DataContext { get; set; } = null!;
    }
}