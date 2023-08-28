using System.Collections.Specialized;

namespace Scarab.Util;

internal static class EventArgsCache
{
    internal static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged = new (NotifyCollectionChangedAction.Reset);
}

internal class SortableObservableCollection<T> : ObservableCollection<T>
{
    public SortableObservableCollection(IEnumerable<T> iter) : base(iter) {}
        
    public void SortBy(Func<T, T, int> comparer)
    {
        // This shouldn't ever change due to binary serialization constraints.
        if (Items is not List<T> items)
            throw new InvalidOperationException("The backing field type is not List<T> on Collection<T>.");

        items.Sort((x, y) => comparer(x, y));
            
        OnCollectionChanged(EventArgsCache.ResetCollectionChanged);
    }
}