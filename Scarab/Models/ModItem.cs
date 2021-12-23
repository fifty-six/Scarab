using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using PropertyChanged.SourceGenerator;
using Scarab.Interfaces;

namespace Scarab.Models
{
    public partial class ModItem : INotifyPropertyChanged, IEquatable<ModItem>
    {
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
            if (dependencies.Length > 0)
            {
                Description += "\n\nDependencies:\n" + String.Join(", ", dependencies);
            }
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

        public async Task OnInstall(IInstaller inst, Action<ModProgressArgs> setProgress)
        {
            if (State is InstalledState(var enabled, var updated))
            {
                // If we're not updated, update
                if (!updated)
                {
                    await inst.Install(this, setProgress, enabled);
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

                setProgress(new ModProgressArgs());

                await inst.Install(this, setProgress, true);

                setProgress(new ModProgressArgs {
                    Completed = true
                });
            }
        }
        
        #region Equality
        public bool Equals(ModItem? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            
            return _state.Equals(other._state)
                && Version.Equals(other.Version)
                && Dependencies.Zip(other.Dependencies).All(tuple => tuple.First == tuple.Second)
                && Link == other.Link
                && Name == other.Name
                && Description == other.Description;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            
            return obj.GetType() == GetType() && Equals((ModItem) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Version, Dependencies, Link, Name, Description);
        }

        public static bool operator ==(ModItem? left, ModItem? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ModItem? left, ModItem? right)
        {
            return !Equals(left, right);
        }
        #endregion
    }
}