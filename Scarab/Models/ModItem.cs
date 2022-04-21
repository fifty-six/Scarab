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
            string shasum,
            string name,
            string description
        )
        {
            _state = state;

            Sha256 = shasum;
            Version = version;
            Dependencies = dependencies;
            Link = link;
            Name = name;
            Description = description;

            DependenciesDesc = string.Join(Environment.NewLine, Dependencies);
        }

        public Version  Version          { get; }
        public string[] Dependencies     { get; }
        public string   Link             { get; }
        public string   Sha256           { get; }
        public string   Name             { get; }
        public string   Description      { get; }
        public string   DependenciesDesc { get; }

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

        public string InstallText => State switch
        {
            InstalledState { Updated: false } => "Update",
            InstalledState { Updated: true } => "Uninstall",
            NotInstalledState => "Install",
            _ => throw new InvalidOperationException("Unreachable")
        };

        public bool Installed => State is InstalledState;

        public bool HasDependencies  => Dependencies.Length > 0;

        public bool UpdateAvailable => _state is InstalledState s && s.Version < Version;

        public string UpdateText  => $"\u279E {Version}";

        public string VersionText => _state switch
        {
            InstalledState st => st.Version.ToString(),
            NotInstalledState => Version.ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(_state))
        };

        // Gray out the Enabled text if the mod isn't installed and we can't enable/disable.
        public Color EnabledColor => State is InstalledState
            ? Color.Parse("#ffdedede")
            : Color.Parse("#6d6d6d");

        public async Task OnInstall(IInstaller inst, Action<ModProgressArgs> setProgress)
        {
            ModState origState = State;
            
            try
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

                    setProgress
                    (
                        new ModProgressArgs
                        {
                            Completed = true
                        }
                    );
                }
            }
            catch
            {
                State = origState;
                throw;
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