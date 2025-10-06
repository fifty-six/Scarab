using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Scarab.Views;

namespace Scarab;

[UsedImplicitly]
public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        #if DEBUG
        this.AttachDevTools();
        #endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}