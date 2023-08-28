using System;
using System.Diagnostics;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Splat.Microsoft.Extensions.Logging;

namespace Scarab.Util;

public static class ContainerExt
{
    public static void AddLogging(this Container con)
    {
        var sc = new ServiceCollection();

        sc.AddLogging(l =>
        {
            l.AddSplat();
            l.AddConsole();
            
            foreach (TraceListener listener in Trace.Listeners)
                l.AddTraceSource("trace", listener);

            l.AddDebug();
        });

        con.Register(
            Made.Of(
                _ => ServiceInfo.Of<ILoggerFactory>(),
                f => f.CreateLogger(Arg.Index<Type>(0)),
                r => r.Parent.ImplementationType
            )
        );

        con.RegisterInstance(sc.BuildServiceProvider().GetRequiredService<ILoggerFactory>());
    }

}