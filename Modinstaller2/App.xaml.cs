using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Modinstaller2.Services;
using Modinstaller2.ViewModels;
using Modinstaller2.Views;
using System;

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

                // hey moron make the disabled folder

                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(db),
                };
            }
            else throw new NotImplementedException();

            base.OnFrameworkInitializationCompleted();
        }
    }
}
