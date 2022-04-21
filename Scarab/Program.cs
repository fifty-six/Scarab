using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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

            AppDomain.CurrentDomain.ProcessExit += (_, _) => Handler(null);
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, Handler);
            PosixSignalRegistration.Create(PosixSignal.SIGINT, Handler);
            Console.CancelKeyPress += (_, _) => Handler(null);
            
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception e)
            {
                WriteExceptionToLog(e);
            }
        }

        private static void Handler(PosixSignalContext? c)
        {
            Trace.WriteLine("Something sent a shutdown event, calling Application.Shutdown");
            
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            
            Trace.WriteLine("Got past Application.Shutdown, did shutting down fail?");
        }

        private static void SetupLogging()
        {
            var fileListener = new TextWriterTraceListener
            (
                Path.Combine
                (
                    Settings.GetOrCreateDirPath(),
                    "ModInstaller.log"
                )
            );

            fileListener.TraceOutputOptions = TraceOptions.DateTime;

            Trace.AutoFlush = true;

            Trace.Listeners.Add(fileListener);

            AppDomain.CurrentDomain.UnhandledException += (_, eArgs) =>
            {
                // Can't open a UI as this is going to crash, so we'll save to a log file.
                WriteExceptionToLog((Exception) eArgs.ExceptionObject);
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

            Trace.TraceError(e.ToString());

            File.WriteAllText(dir + $"ModInstaller_Error_{date}.log", e.ToString());
            File.WriteAllText(Path.Combine(Settings.GetOrCreateDirPath(), $"ModInstaller_Error_{date}.log"), e.ToString());

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