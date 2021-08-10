using Avalonia.Controls;

namespace Modinstaller2.Views
{
    public class View<T> : UserControl where T : class
    {
        public new T DataContext { get; set; } = null!;
    }
}