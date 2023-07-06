using System;
using System.ComponentModel;
using System.Linq;
using System.Resources;
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
            string description,
            string repository,
            string[] tags,
            string[] integrations
        )
        {
            _state = state;

            Sha256 = shasum;
            Version = version;
            Dependencies = dependencies;
            Link = link;
            Name = name;
            Description = description;
            Repository = repository;
            Tags = tags;
            Integrations = integrations;

            DependenciesDesc = string.Join(Environment.NewLine, Dependencies);
            TagDesc          = string.Join(Environment.NewLine, Tags);
            IntegrationsDesc = string.Join(Environment.NewLine, Integrations);
        }


        public Version  Version          { get; }
        public string[] Dependencies     { get; }
        public string   Link             { get; }
        public string   Sha256           { get; }
        public string   Name             { get; }
        public string   Description      { get; }
        public string   Repository       { get; }
        
        public string[] Tags { get; }
        public string[] Integrations { get; }
        
        public string   DependenciesDesc { get; }
        public string   TagDesc          { get; }
        public string   IntegrationsDesc { get; }

        [Notify]
        private ModState _state;

        public bool EnabledIsChecked => State switch
        {
            InstalledState { Enabled: var x } => x,

            // Can't enable what isn't installed.
            _ => false
        };

        public bool Installing => State is NotInstalledState { Installing: true };

        public string InstallText => State switch
        {
            InstalledState { Updated: false } => Resources.XAML_Update,
            InstalledState => Resources.MI_InstallText_Installed,
            NotInstalledState => Resources.MI_InstallText_NotInstalled,
            _ => throw new InvalidOperationException("Unreachable")
        };
        
        public string InstallIcon => State switch
        {
            InstalledState { Updated: false } => "fa-solid fa-rotate",
            InstalledState => "fa-solid fa-trash-can",
            NotInstalledState => "fa-solid fa-download",
            _ => throw new InvalidOperationException("Unreachable")
        };


        public bool Installed => State is InstalledState;

        public bool HasDependencies => Dependencies.Length > 0;
        public bool HasIntegrations => Integrations.Length > 0;
        public bool HasTags => Tags.Length > 0;

        public bool UpdateAvailable => State is InstalledState s && s.Version < Version;

        public string UpdateText  => $"\u279E {Version}";

        public string VersionText => State switch
        {
            InstalledState st => st.Version.ToString(),
            NotInstalledState => Version.ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(_state))
        };

        public string InstallFg => State switch
        {
            InstalledState { Updated: false } => "Warning",
            InstalledState => "Danger",
            _ => "Primary"
        };

        public async Task OnUpdate(IInstaller inst, Action<ModProgressArgs> setProgress)
        {
            ModState orig = State;

            try
            {
                if (State is not InstalledState { Updated: false, Enabled: var enabled })
                    throw new InvalidOperationException("Not able to be updated!");

                setProgress(new ModProgressArgs());

                await inst.Install(this, setProgress, enabled);
                
                setProgress(new ModProgressArgs { Completed = true });
            }
            catch
            {
                State = orig;
                throw;
            }
        }

        public async Task OnInstall(IInstaller inst, Action<ModProgressArgs> setProgress)
        {
            ModState origState = State;
            
            try
            {
                if (State is InstalledState)
                {
                    await inst.Uninstall(this);
                }
                else
                {
                    State = (NotInstalledState) State with { Installing = true };

                    setProgress(new ModProgressArgs());

                    await inst.Install(this, setProgress, true);

                    setProgress(new ModProgressArgs { Completed = true });
                }
            }
            catch
            {
                State = origState;
                throw;
            }
        }
        
        public void CallOnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
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