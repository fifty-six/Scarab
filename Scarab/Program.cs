using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Svg;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Splat;
using Splat.Serilog;

namespace Scarab;

[UsedImplicitly]
internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    public static void Main(string[] args)
    {
        SetupLogging();
        SetupExceptionHandling();
        
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
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eArgs) =>
        {
            // Can't open a UI as this is going to crash, so we'll save to a log file.
            WriteExceptionToLog((Exception) eArgs.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (_, eArgs) => { WriteExceptionToLog(eArgs.Exception); };
    }

    private static void Handler(PosixSignalContext? c) => Environment.Exit(-1);

    private static void SetupLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel
#if DEBUG
            .Debug()
#else
            .Information()
#endif
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(Settings.GetOrCreateDirPath(), "ModInstaller-.log"),
                rollingInterval: RollingInterval.Day
            )
            .CreateLogger();

        Locator.CurrentMutable.UseSerilogFullLogger();

        Log.Logger.Information("Launching...");
    }

    private static void WriteExceptionToLog(Exception e)
    {
        if (Debugger.IsAttached)
            Debugger.Break();

        Log.Logger.Fatal(e, "Fatal error!");
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();
            
        // Used to make Avalonia.Svg.Skia controls work in the previewer
#if DEBUG
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(Avalonia.Svg.Svg).Assembly);
#endif
        
        return AppBuilder.Configure<App>()
                         .UsePlatformDetect()
                         .WithInterFont()
                         .UseSkia()
                         .With(new FontManagerOptions
                         {
                             DefaultFamilyName = "avares://Avalonia.Fonts.Inter/Assets#Inter"
                         })
                         .UseReactiveUI();
    }
}