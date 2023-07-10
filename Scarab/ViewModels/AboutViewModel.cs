using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reflection;
using ReactiveUI;

namespace Scarab.ViewModels;

public class AboutViewModel : ViewModelBase
{
    public ReactiveCommand<Unit, Unit> Donate { get; set; }
    public ReactiveCommand<Unit, Unit> OpenLogs { get; set; }

    public AboutViewModel()
    {
        Donate = ReactiveCommand.Create(_Donate);
        OpenLogs = ReactiveCommand.Create(_OpenLogs);
    }

    public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

    public string OSString => $"{OS} {Environment.OSVersion.Version}";

    [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
    private static string OS
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return "Windows";
            if (OperatingSystem.IsMacOS())
                return "macOS";
            if (OperatingSystem.IsLinux())
                return "Linux";
            return "Unknown";
        }
    }

    private static void _Donate() 
    {
        Process.Start(new ProcessStartInfo("https://paypal.me/ybham") { UseShellExecute = true });
    }

    private static void _OpenLogs()
    {
        Process.Start(new ProcessStartInfo(Settings.GetOrCreateDirPath()) { UseShellExecute = true });
    }
}