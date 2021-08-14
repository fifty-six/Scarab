using System;
using System.Diagnostics;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MessageBox.Avalonia.BaseWindows.Base;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Scarab.Interfaces;
using Scarab.Services;
using Scarab.Util;

namespace Scarab.ViewModels
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

            Settings settings = Settings.Load() ?? Settings.Create(await GetSettingsPath());

            sc.AddSingleton<ISettings>(_ => settings)
              .AddSingleton<IModSource>(_ => InstalledMods.Load())
              .AddSingleton<IModDatabase, ModDatabase>()
              .AddSingleton<IInstaller, Installer>()
              .AddSingleton<ModListViewModel>();
            
            ServiceProvider sp = sc.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true
            });

            Content = sp.GetRequiredService<ModListViewModel>();
        }

        private static async Task<string> GetSettingsPath()
        {
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
                
                return await SelectPathUtil.SelectPath();
            }

            Trace.WriteLine($"Settings doesn't exist. Creating it at detected path {path}.");

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

            return res == ButtonResult.Yes
                ? path
                : await SelectPathUtil.SelectPath();
        }

        public MainWindowViewModel() => Task.Run(async () => 
        {
            try
            {
                await Impl();
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.StackTrace);
                Trace.Flush();

                if (Debugger.IsAttached)
                    Debugger.Break();
                
                Environment.Exit(-1);
                
                throw;
            }
        });
    }
}