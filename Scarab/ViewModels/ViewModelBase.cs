using ReactiveUI;

namespace Scarab.ViewModels
{
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
}
