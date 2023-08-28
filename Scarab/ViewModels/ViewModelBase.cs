using System.Diagnostics.CodeAnalysis;

namespace Scarab.ViewModels;

// INPC022, INPC031 - Needed for dependencies on base properties
[SuppressMessage("ReSharper", "VirtualMemberNeverOverridden.Global")]
public class ViewModelBase : ReactiveObject
{
    protected virtual void RaisePropertyChanged(string name)
    {
        IReactiveObjectExtensions.RaisePropertyChanged(this, name);
    }

    protected virtual void RaisePropertyChanging(string name)
    {
        IReactiveObjectExtensions.RaisePropertyChanging(this, name);
    }
}