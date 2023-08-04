using System.Linq;
using System.Threading.Tasks;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.ViewModels;

namespace Scarab.Mock;

public class DesignModPageViewModel : ModPageViewModel
{
    public DesignModPageViewModel(ISettings settings,
        IModDatabase db,
        IInstaller inst,
        IModSource mods) : base(settings, db, inst, mods)
    {
        Database = db;
    }

    public IModDatabase Database { get; }
}

public static class MockViewModel
{
    public static DesignModPageViewModel DesignInstance
    {
        get
        {
            var src = new Moq.Mock<IModSource>();
            src.SetupGet(x => x.ApiInstall).Returns(new NotInstalledState());

            var db = new MockDatabase();

            return new DesignModPageViewModel(Moq.Mock.Of<ISettings>(), db, Moq.Mock.Of<IInstaller>(), src.Object) 
            {
                SelectedModItem = db.Items.ToList()[0]
            };
        }
    }
    
    public static ModItem DesignMod => new MockDatabase().Items.ToArray()[0];

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