using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Svg.Skia;
using JetBrains.Annotations;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

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

            PosixSignalRegistration.Create(PosixSignal.SIGTERM, Handler);
            PosixSignalRegistration.Create(PosixSignal.SIGINT, Handler);
            
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception e)
            {
                WriteExceptionToLog(e);
            }
        }

        private static void Handler(PosixSignalContext? c) => Environment.Exit(-1);

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
        private static AppBuilder BuildAvaloniaApp()
        {
            IconProvider.Current.Register<FontAwesomeIconProvider>();
            
            // Used to make Avalonia.Svg.Skia controls work in the previewer
            #if DEBUG
            GC.KeepAlive(typeof(SvgImageExtension).Assembly);
            GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);
            #endif
            
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .With(new FontManagerOptions
                {
                    DefaultFamilyName = "avares://Avalonia.Fonts.Inter/Assets#Inter"
                })
                .LogToTrace()
                .UseReactiveUI();
        }
    }
}