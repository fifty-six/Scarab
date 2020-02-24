using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Modinstaller2.Services;
using Modinstaller2.ViewModels;
using Modinstaller2.Views;
using System;
using System.IO;

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

#warning TODO: Move to after Settings initialization (MainWindowViewModel?)
                // TODO: This should be done after prompting the user, otherwise it *will* throw.
                // if (!Directory.Exists(InstallerSettings.Instance.DisabledFolder))
                //    Directory.CreateDirectory(InstallerSettings.Instance.DisabledFolder);

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
