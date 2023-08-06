using ReactiveUI;

namespace Scarab.ViewModels
{
    public class ViewModelBase : ReactiveObject
    {
        protected void RaisePropertyChanged(string name)
        {
            IReactiveObjectExtensions.RaisePropertyChanged(this, name);
        }

        protected void RaisePropertyChanging(string name)
        {
            IReactiveObjectExtensions.RaisePropertyChanging(this, name);
        }
    }
}
