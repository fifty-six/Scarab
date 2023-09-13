using System.ComponentModel;
using System.Diagnostics;

namespace Scarab.Models;

public sealed partial record ModItem : INotifyPropertyChanged
{
    public ModItem(ModState state,
        Version version,
        string[] dependencies,
        string link,
        string shasum,
        string name,
        string description,
        string repository,
        ImmutableArray<Tag> tags,
        string[] integrations,
        string[] authors
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
        Tags = tags.Aggregate((Tag) 0, (acc, x) => acc | x);
        Integrations = integrations;
        Authors = authors;
    }

    // Install details
    public string Name { get; }
    public Version Version { get; }
    public string[] Dependencies { get; }
        
    // Download details
    public string Link { get; }
    public string Sha256 { get; }
        
    // Displayed info
    public Tag Tags { get; }
    public string Description { get; }
    public string Repository { get; }
    public string[] Integrations { get; }
    public string[] Authors { get; }

    [Notify] 
    private ModState _state;

    public bool Enabled => State is InstalledState { Enabled: true };

    public bool Installed => State is InstalledState;

    public bool UpdateAvailable => State is InstalledState s && s.Version < Version;

    public string VersionText => State switch
    {
        InstalledState st => st.Version.ToString(),
        NotInstalledState => Version.ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(_state))
    };

    public void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Repository) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Error(e, "Unable to open repository!");
        }
    }

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
                if (State is not InstalledState { Updated: false, Enabled: var enabled })
                    throw new InvalidOperationException("Not able to be updated!");

                setProgress(new ModProgressArgs());

                await inst.Install(this, setProgress, enabled);

                setProgress(new ModProgressArgs { Completed = true });
            }
            else
            {
                State = new NotInstalledState { Installing = true };

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

    public async Task OnUninstall(IInstaller inst, Action<ModProgressArgs> setProgress)
    {
        ModState origState = State;

        try
        {
            await inst.Uninstall(this);
        }
        catch
        {
            State = origState;
            throw;
        }
    }

    // Needs to run on the UI thread, as property changed events
    // cause *UI* changes, and so otherwise we'll get an invalid
    // thread access exception
    private void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.Invoke(() => PropertyChanged?.Invoke(this, e));
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

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Name);
        hashCode.Add(Version);
        hashCode.Add(Dependencies);
        hashCode.Add(Link);
        hashCode.Add(Sha256);
        hashCode.Add((int) Tags);
        hashCode.Add(Description);
        hashCode.Add(Repository);
        hashCode.Add(Integrations);
        hashCode.Add(Authors);
        return hashCode.ToHashCode();
    }
    #endregion

    public override string ToString()
    {
        return $"ModItem {{ {nameof(Name)}: {Name}, {nameof(State)}: {State}, {nameof(Version)}: {Version} }}";
    }
}