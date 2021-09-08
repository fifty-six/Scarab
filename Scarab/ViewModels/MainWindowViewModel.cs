using System;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MessageBox.Avalonia.BaseWindows.Base;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.Services;
using Scarab.Util;

#if !DEBUG
using System.Text.Json;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Models;
#endif

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
            await CheckUpToDate();
            
            var sc = new ServiceCollection();

            Settings settings = Settings.Load() ?? Settings.Create(await GetSettingsPath());

            (ModLinks, ApiLinks) content = await ModDatabase.FetchContent();
            
            sc.AddSingleton<ISettings>(_ => settings)
              .AddSingleton<IFileSystem, FileSystem>()
              .AddSingleton<IModSource>(_ => InstalledMods.Load())
              .AddSingleton<IModDatabase, ModDatabase>(sp => new ModDatabase(sp.GetRequiredService<IModSource>(), content))
              .AddSingleton<IInstaller, Installer>()
              .AddSingleton<ModListViewModel>();
            
            ServiceProvider sp = sc.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true
            });

            Content = sp.GetRequiredService<ModListViewModel>();
        }

        private static 
            #if !DEBUG
            async 
            #endif
            Task CheckUpToDate()
        {
            Version? current_version = Assembly.GetExecutingAssembly().GetName().Version;
            
            Debug.WriteLine($"Current version of installer is {current_version}");
            
            #if DEBUG
            return Task.CompletedTask;
            #else
            const string gh_releases = "https://api.github.com/repos/fifty-six/Scarab/releases/latest";


            string json;
            
            try
            {
                var wc = new WebClient();

                json = await wc.DownloadStringTaskAsync(new Uri(gh_releases));
            }
            catch (WebException) {
                return;
            }

            JsonDocument doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("tag", out var tag_elem))
                return;

            string? tag = tag_elem.GetString();

            if (tag is null)
                return;

            if (tag.StartsWith("v"))
                tag = tag[1..];

            if (!Version.TryParse(tag, out Version? version))
                return;

            if (version <= current_version)
                return;
            
            string? res = await MessageBoxManager.GetMessageBoxCustomWindow
            (
                new MessageBoxCustomParams {
                    ButtonDefinitions = new[] {
                        new ButtonDefinition {
                            IsDefault = true,
                            IsCancel = true,
                            Name = "Get the latest release"
                        },
                        new ButtonDefinition {
                            Name = "Continue anyways."
                        }
                    },
                    ContentTitle = "Out of date!",
                    ContentMessage = "This program is out of date! It may not function correctly.",
                    SizeToContent = SizeToContent.WidthAndHeight
                }
            ).Show();

            if (res == "Get the latest release")
            {
                Process.Start(new ProcessStartInfo("https://github.com/fifty-six/Scarab/releases/latest") { UseShellExecute = true });
                
                ((IClassicDesktopStyleApplicationLifetime) Application.Current.ApplicationLifetime).Shutdown();
            }
            else
            {
                Trace.WriteLine($"Installer out of date! Version {current_version} with latest {version}!");
            }
            #endif
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