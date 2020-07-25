using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Threading;
using MessageBox.Avalonia.BaseWindows;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
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
                SelectPath();
            }
            else
            {
                if (InstallerSettings.SettingsExists)
                {
                    SwapToModlist();
                    
                    return;
                }

                Debug.WriteLine($"Settings doesn't exist. Creating it at detected path {path}.");

                IMsBoxWindow<ButtonResult> window = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow
                (
                    new MessageBoxStandardParams
                    {
                        ContentHeader = "Detected path!",
                        ContentMessage = $"Detected Hollow Knight install at {path}. Is this correct?",
                        ButtonDefinitions = ButtonEnum.YesNo
                    }
                );

                Dispatcher.UIThread.InvokeAsync
                (
                    async () =>
                    {
                        ButtonResult res = await window.Show();

                        if (res == ButtonResult.Yes)
                        {
                            InstallerSettings.CreateInstance(path);

                            SwapToModlist();
                        }
                        else
                        {
                            SelectPath();
                        }
                    }
                );
            }
        }

        public void SelectPath()
        {
            // Swap view to SelectPathView, but only if we can't autodetect it..
            Debug.WriteLine("Going to SelectPathViewModel.");

            Content = new SelectPathViewModel();

            Content.PropertyChanged += SelectPathChanged;
        }

        public void SwapToModlist()
        {
            _db = new Database();

            Content = new ModListViewModel(_db.Items);
        }

        private void SelectPathChanged(object sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"e: {e}");
            Debug.WriteLine($"e.PropertyName: {e.PropertyName}");

            if (e.PropertyName != "Path" || !(Content is SelectPathViewModel content))
                return;

            Content.PropertyChanged -= SelectPathChanged;

            Debug.WriteLine($"Content: {content.Path}");

            InstallerSettings.CreateInstance(content.Path);

            SwapToModlist();
        }
    }
}