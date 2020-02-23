using Modinstaller2.Services;

namespace Modinstaller2.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ModList List { get; }

        public MainWindowViewModel(Database db)
        {
            List = new ModList(db.GetItems());
        }
    }
}
