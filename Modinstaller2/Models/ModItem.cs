using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Modinstaller2.Interfaces;
using Modinstaller2.Services;
using PropertyChanged.SourceGenerator;

namespace Modinstaller2.Models
{
    public partial class ModItem : INotifyPropertyChanged
    {
        private static readonly SemaphoreSlim _InstallSem = new(1);

        public ModItem
        (
            ModState state,
            Version version,
            string[] dependencies,
            string link,
            string name,
            string description
        )
        {
            _state = state;

            Version = version;
            Dependencies = dependencies;
            Link = link;
            Name = name;
            Description = description;
        }
        
        public  Version  Version      { get; }
        public  string[] Dependencies { get; }
        public  string   Link         { get; }
        public  string   Name         { get; }
        public  string   Description  { get; }

        [Notify]
        private ModState _state;

        public bool EnabledIsChecked =>
            State switch
            {
                InstalledState { Enabled: var x } => x,

                // Can't enable what isn't installed.
                _ => false
            };

        // 
        // Update required -> null
        // Installed -> true
        // Not installed -> false
        // Installing -> true, but different color.
        //
        // We use null for updates so we get 
        // a box in the UI, which is a nice indicator.
        public bool? InstalledIsChecked =>
            State switch
            {
                InstalledState { Updated: true } => true,
                InstalledState { Updated: false } => null,
                NotInstalledState { Installing: true } => true,
                _ => false
            };

        public bool Installing => State is NotInstalledState { Installing: true };

        public Color Color => Color.Parse(State is InstalledState { Updated : true } ? "#ff086f9e" : "#f49107");

        public string InstallText => State is InstalledState { Updated: false } ? "Out of date!" : "Installed?";

        public bool Installed => State is InstalledState;

        public async Task OnInstall(IInstaller inst, Action<bool> setProgressBar, Action<double> setProgress)
        {
            if (State is InstalledState(var enabled, var updated))
            {
                // If we're not updated, update
                if (!updated)
                {
                    setProgressBar(true);

                    await inst.Install(this, setProgress, enabled);

                    setProgressBar(false);
                }
                // Otherwise the user wanted to uninstall.
                else
                {
                    await inst.Uninstall(this);
                }
            }
            else
            {
                State = (NotInstalledState) State with { Installing = true };

                setProgressBar(true);

                await _InstallSem.WaitAsync();

                await inst.Install(this, setProgress, true);

                setProgressBar(false);
            }
        }
    }
}