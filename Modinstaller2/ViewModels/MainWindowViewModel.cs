using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using JetBrains.Annotations;
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

        [UsedImplicitly]
        private ViewModelBase Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }

        public MainWindowViewModel()
        {
            if (InstallerSettings.Instance is not null)
            {
                SwapToModlist();

                return;
            }

            bool autoDetected = InstallerSettings.TryAutoDetect(out string path);

            if (!autoDetected)
            {
                IMsBoxWindow<ButtonResult> info = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow
                (
                    new MessageBoxStandardParams
                    {
                        ContentHeader = "Info",
                        ContentMessage = "Unable to detect your Hollow Knight installation. Please select it."
                    }
                );

                Dispatcher.UIThread.InvokeAsync
                (
                    async () =>
                    {
                        await info.Show();
                        await SelectPath();
                    }
                );

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
                        await SelectPath();
                    }
                }
            );
        }

        private async Task SelectPath()
        {
            string path = await SelectPathUtil.SelectPath();

            InstallerSettings.CreateInstance(path);

            SwapToModlist();
        }

        private void SwapToModlist()
        {
            _db = Database.FromUrl(Database.MODLINKS_URI);

            Content = new ModListViewModel(_db.Items);
        }
    }
}