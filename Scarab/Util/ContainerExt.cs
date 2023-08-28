using System;
using DryIoc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Scarab.Util;

public static class ContainerExt
{
    public static void AddLogging(this Container con)
    {
        var sc = new ServiceCollection();

        sc.AddLogging(l => l.AddSerilog());

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