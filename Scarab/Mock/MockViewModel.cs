using System.Threading.Tasks;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.ViewModels;
using Scarab.Views;

namespace Scarab.Mock;

public static class MockViewModel
{
    public static ModPageViewModel DesignInstance
    {
        get
        {
            var src = new Moq.Mock<IModSource>();
            src.SetupGet(x => x.ApiInstall).Returns(new NotInstalledState());
            
            return new ModPageViewModel(Moq.Mock.Of<ISettings>(), new MockDatabase(), Moq.Mock.Of<IInstaller>(), src.Object);
        }
    }

    
    
    public static AboutViewModel AboutInstance { get; } = new();

    public static SettingsViewModel SettingsInstance
    {
        get
        {
            var settings = new Moq.Mock<ISettings>();
            settings.SetupGet(x => x.ManagedFolder).Returns("/home/home/src/test/Managed");

            return new SettingsViewModel(settings.Object);
        }
    }

    public class MockSource : IModSource
    {
        public ModState ApiInstall { get; } = new NotInstalledState();
        
        public Task RecordApiState(ModState st) { throw new System.NotImplementedException(); }

        public ModState FromManifest(Manifest manifest) { throw new System.NotImplementedException(); }

        public Task RecordInstalledState(ModItem item) { throw new System.NotImplementedException(); }

        public Task RecordUninstall(ModItem item) { throw new System.NotImplementedException(); }

        public Task Reset() { throw new System.NotImplementedException(); }
    }
}