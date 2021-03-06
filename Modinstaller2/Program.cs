using System;
using System.IO;
using Avalonia;
using Avalonia.Logging.Serilog;
using Avalonia.ReactiveUI;
using JetBrains.Annotations;

namespace Modinstaller2
{
    [UsedImplicitly]
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, eArgs) =>
            {
                // Can't open a UI as this is going to crash, so we'll save to a log file.
                File.WriteAllText($"ModInstaller_Error_{DateTime.Now:s}.log", eArgs.ExceptionObject.ToString());
            };

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        private static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .LogToDebug()
                      .UseReactiveUI();
    }
}