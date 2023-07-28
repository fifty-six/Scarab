using System.Diagnostics;
using ReactiveUI;

namespace Scarab.ViewModels
{
    public class ViewModelBase : ReactiveObject
    {
        protected virtual void RaisePropertyChanged(string name)
        {
            Trace.WriteLine($"Property {name} on type {this.GetType().Name}");
            IReactiveObjectExtensions.RaisePropertyChanged(this, name);
        }

        protected virtual void RaisePropertyChanging(string name)
        {
            Trace.WriteLine($"Property {name} on type {this.GetType().Name}");
            IReactiveObjectExtensions.RaisePropertyChanging(this, name);
        }
    }
}
