using FakeItEasy;

namespace Scarab.Mock;

public class DesignModPageViewModel : ModPageViewModel
{
    public DesignModPageViewModel(ISettings settings,
        IModDatabase db,
        IInstaller inst,
        IModSource mods) : base(settings, db, inst, mods, A.Fake<ILogger>(), new HttpClient())
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
            var src = A.Fake<IModSource>();
            A.CallTo(() => src.ApiInstall).Returns(new NotInstalledState());

            var db = new MockDatabase();

            return new DesignModPageViewModel(A.Fake<ISettings>(), db, A.Fake<IInstaller>(), src) 
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
            var settings = A.Fake<ISettings>();
            A.CallTo(() => settings.ManagedFolder).Returns("/home/home/src/test/Managed");

            return new SettingsViewModel(settings, A.Fake<IModSource>());
        }
    }
}