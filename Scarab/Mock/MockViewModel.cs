using System.Linq;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.ViewModels;

namespace Scarab.Mock;

public class DesignModPageViewModel(
        ISettings settings,
        IModDatabase db,
        IInstaller inst,
        IModSource mods
    )
    : ModPageViewModel(settings, db, inst, mods)
{
    public IModDatabase Database { get; } = db;
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
}