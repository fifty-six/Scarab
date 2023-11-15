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

public static class MockPathViewModel
{
    public static PathViewModel DesignInstance =>
        new PathViewModel(new SuffixNotFoundError("/home/home/Downloads", PathUtil.SUFFIXES));
};

public static class MockModPageViewModel
{
    public static DesignModPageViewModel DesignInstance
    {
        get
        {
            ModState apiInstall = new NotInstalledState();
                //new InstalledState(true, Version: new Version(1, 0, 0, 0), true);
            
            var src = A.Fake<IModSource>();
            A.CallTo(() => src.ApiInstall).ReturnsLazily(() => apiInstall);
            
            var installer = A.Fake<IInstaller>();
            
            A.CallTo(() => installer.ToggleApi())
             .ReturnsLazily(
                 () =>
                 {
                     apiInstall = apiInstall switch
                     {
                         InstalledState(true, _, _) i => i with { Enabled = false },
                         InstalledState(false, _, _) i => i with { Enabled = true },
                         _ => throw new ArgumentOutOfRangeException(nameof(apiInstall))
                     };

                     return Task.CompletedTask;
                 }
             );
            A.CallTo(() => installer.InstallApi(IInstaller.ReinstallPolicy.SkipUpToDate))
             .ReturnsLazily(
                 () =>
                 {
                     apiInstall = new InstalledState(true, new Version(1, 0), true);
                     return Task.CompletedTask;
                 }
             );

            var db = new MockDatabase();

            return new DesignModPageViewModel(A.Fake<ISettings>(), db, installer, src) 
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
            A.CallTo(() => settings.ManagedFolder).Returns("/home/home/src/test/hollow_knight_Data/Managed");

            return new SettingsViewModel(settings, A.Fake<IModSource>());
        }
    }
}