using Modinstaller2.Models;
using Modinstaller2.Services;
using ReactiveUI;
using System;
using System.ComponentModel;

namespace Modinstaller2.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase _content;
        private readonly Database _db;

        private ViewModelBase Content
        {
            get => _content;
            set 
            {
                this.RaiseAndSetIfChanged(ref _content, value);
            }
        }

        public MainWindowViewModel(Database db)
        {
            _db = db;

#warning TODO: Use AutoDetect and if detected use Avalonia Notifications. https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/ViewModels/MainWindowViewModel.cs#L23
            if (!InstallerSettings.SettingsExists && !InstallerSettings.TryAutoDetect(out string path))
            {
                // Swap view to SelectPathView, but only if we can't autodetect it..
                System.Diagnostics.Debug.WriteLine("Going to SelectPathViewModel.");

                Content = new SelectPathViewModel();

                Content.PropertyChanged += SelectPathChanged;
            }
            else
            {
                Content = new ModListViewModel(db.GetItems());
            }
        }

        private void SelectPathChanged(object sender, PropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(e);
            System.Diagnostics.Debug.WriteLine(e.PropertyName);

            if (e.PropertyName == "Path")
            {
                Content.PropertyChanged -= SelectPathChanged;

                Content = new ModListViewModel(_db.GetItems());
            }
        }
    }
}
