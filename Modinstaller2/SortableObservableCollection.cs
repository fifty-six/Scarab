using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using System.Text;

namespace Modinstaller2
{
    internal class SortableObservableCollection<T> : ObservableCollection<T>
    {
        public SortableObservableCollection(IEnumerable<T> iter) : base(iter) {}
        
        public void SortBy(Func<T, T, int> comparer)
        {
            if (!(Items is List<T> items))
            {
                throw new InvalidOperationException("The backing field type is not List<T> on Collection<T>.");
            }

            items.Sort((x, y) => comparer(x, y));

            typeof(ObservableCollection<T>).GetMethod("OnCollectionReset", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(this, new object[0]);
        }
    }
}
