using System.ComponentModel;
using System.Diagnostics;
using Modinstaller2.Services;
using ReactiveUI;

namespace Modinstaller2.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase _content;
        private Database _db;

        private ViewModelBase Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }

        public MainWindowViewModel()
        {
            string path = null;
            
            if (!InstallerSettings.SettingsExists && !InstallerSettings.TryAutoDetect(out path))
            {
                // Swap view to SelectPathView, but only if we can't autodetect it..
                Debug.WriteLine("Going to SelectPathViewModel.");

                Content = new SelectPathViewModel();

                Content.PropertyChanged += SelectPathChanged;
            }
            else
            {
                if (!InstallerSettings.SettingsExists)
                {
                    Debug.WriteLine($"Settings doesn't exist. Creating it at detected path {path}.");

                    InstallerSettings.CreateInstance(path);
                } 
                else
                {
                    Debug.WriteLine("Settings exists.");
                }

                _db = new Database();

                Content = new ModListViewModel(_db.Items);
            }
        }

        private void SelectPathChanged(object sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"e: {e}");
            Debug.WriteLine($"e.PropertyName: {e.PropertyName}");

            if (e.PropertyName == "Path" && Content is SelectPathViewModel content)
            {
                Content.PropertyChanged -= SelectPathChanged;

                Debug.WriteLine($"Content: {content.Path}");

                InstallerSettings.CreateInstance(content.Path);

                _db = new Database();

                Content = new ModListViewModel(_db.Items);
            }
        }
    }
}