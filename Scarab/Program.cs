using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.ReactiveUI;
using JetBrains.Annotations;

namespace Scarab
{
    [UsedImplicitly]
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args)
        {
            SetupLogging();
            
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception e)
            {
                WriteExceptionToLog(e);
            }
        }

        private static void SetupLogging()
        {
            using var fileListener = new TextWriterTraceListener
            (
                Path.Combine
                (
                    Settings.GetOrCreateDirPath(),
                    "ModInstaller.log"
                )
            );

            Trace.AutoFlush = true;

            Trace.Listeners.Add(fileListener);

            AppDomain.CurrentDomain.UnhandledException += (_, eArgs) =>
            {
                // Can't open a UI as this is going to crash, so we'll save to a log file.
                WriteExceptionToLog((Exception)eArgs.ExceptionObject);
            };

            TaskScheduler.UnobservedTaskException += (_, eArgs) => { WriteExceptionToLog(eArgs.Exception); };

            Trace.WriteLine("Launching...");
        }

        private static void WriteExceptionToLog(Exception e)
        {
            string date = DateTime.Now.ToString("yy-MM-dd HH-mm-ss");

            string dirName = AppContext.BaseDirectory;

            string dir = dirName switch
            {
                // ModInstaller.app/Contents/MacOS/Executable
                "MacOS" => "../../../",
                _ => string.Empty
            };
            
            if (Debugger.IsAttached)
                Debugger.Break();

            Trace.WriteLine($"Caught exception, writing to log: {e.StackTrace}");

            File.WriteAllText(dir + $"ModInstaller_Error_{date}.log", e.StackTrace);

            Trace.Flush();
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        private static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                      .UsePlatformDetect()
                      .LogToTrace()
                      .UseReactiveUI();
    }
}