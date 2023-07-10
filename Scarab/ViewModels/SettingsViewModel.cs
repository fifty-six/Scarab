using System;
using System.Diagnostics;
using Scarab.Interfaces;

namespace Scarab.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private ISettings Settings { get; }

    public SettingsViewModel()
    {
#if !DEBUG
        throw new InvalidOperationException()
#else
        
        var moq = new Moq.Mock<ISettings>();
        moq.SetupGet(x => x.ManagedFolder).Returns("/home/home/test/Managed");
        Settings = moq.Object;
#endif
    }
    
    public SettingsViewModel(ISettings settings)
    {
        Settings = settings;
    }
}