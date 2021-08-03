using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using JetBrains.Annotations;
using MessageBox.Avalonia.BaseWindows.Base;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using Microsoft.Extensions.DependencyInjection;
using Modinstaller2.Services;
using Modinstaller2.Util;
using ReactiveUI;

namespace Modinstaller2.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase _content = null!;

        [UsedImplicitly]
        private ViewModelBase Content
        {
            get => _content;
            set => this.RaiseAndSetIfChanged(ref _content, value);
        }

        private async Task Impl()
        {
            var sc = new ServiceCollection();

            Settings? settings = Settings.Load();
            
            if (settings is not null)
            {
                sc.AddSingleton<Settings>(_ => settings);
            
                SwapToModlist(sc);

                return;
            }

            if (!Settings.TryAutoDetect(out string? path))
            {
                IMsBoxWindow<ButtonResult> info = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow
                (
                    new MessageBoxStandardParams
                    {
                        ContentHeader = "Info",
                        ContentMessage = "Unable to detect your Hollow Knight installation. Please select it."
                    }
                );

                await info.Show();
                await SelectPath(sc);

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

            ButtonResult res = await window.Show();

            if (res == ButtonResult.Yes)
                SwapToModlist(sc.AddSingleton(_ => Settings.Create(path)));
            else
                await SelectPath(sc);
        }

        // public MainWindowViewModel() => Dispatcher.UIThread.InvokeAsync(Impl);
        public MainWindowViewModel() => Task.Run(async () =>
        {
            try
            {
                await Impl();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);

                Environment.Exit(-1);
                
                throw;
            }
        });

        private async Task SelectPath(IServiceCollection sc)
        {
            string path = await SelectPathUtil.SelectPath();

            SwapToModlist(sc.AddSingleton(_ => Settings.Create(path)));
        }

        private void SwapToModlist(IServiceCollection sc)
        {
            sc
                .AddSingleton(_ => InstalledMods.Load())
                .AddSingleton<ModDatabase>();
            
            Content = new ModListViewModel(sc.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));
        }
    }
}