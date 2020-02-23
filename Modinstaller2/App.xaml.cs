using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Modinstaller2.Services;
using Modinstaller2.ViewModels;
using Modinstaller2.Views;

namespace Modinstaller2
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var db = new Database();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(db),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
