using System;
using System.Diagnostics.Contracts;
using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Scarab.Extensions;

public static class ReactiveExtensions
{
    public static IDisposable WhenActivatedVM<T>(
        this IViewFor<T> self,
        Action<T, CompositeDisposable> act,
        IViewFor? view = null
    )
    where T : class
    {
        return self.WhenActivated(d =>
        {
            ArgumentNullException.ThrowIfNull(self.ViewModel, nameof(self.ViewModel));
            
            act(self.ViewModel, d);
            
        }, view);
    }
}