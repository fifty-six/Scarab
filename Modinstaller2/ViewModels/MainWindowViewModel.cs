using Modinstaller2.Models;
using Modinstaller2.Services;

namespace Modinstaller2.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ModListViewModel List { get; }

        public MainWindowViewModel(Database db)
        {
            List = new ModListViewModel(db.GetItems());
        }
    }
}
