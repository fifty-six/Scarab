using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.Services;
using Xunit;

namespace Scarab.Tests;

public class ModSourceTest
{
    [Fact]
    public async Task Record()
    {
        var fs = new MockFileSystem();
            
        fs.AddDirectory(Path.GetDirectoryName(InstalledMods.ConfigPath));
            
        IModSource ms = new InstalledMods(fs);

        var orig_version = new Version("1.3.2.2");

        var state = new InstalledState(true, orig_version, true);
            
        var item = new ModItem
        (
            state,
            new Version("1.3.2.2"),
            Array.Empty<string>(),
            string.Empty,
            string.Empty,
            "test",
            "test",
            "repo",
            ImmutableArray<Tag>.Empty,
            Array.Empty<string>(),
            Array.Empty<string>()
        );
            
        await ms.RecordInstalledState(item);
            
        Assert.True(fs.FileExists(InstalledMods.ConfigPath));

        var manifest = new Manifest {
            Name = "test"
        };

        ModState up_to_date = ms.FromManifest
        (
            manifest with {
                Version = orig_version
            }
        );

        Assert.Equal(up_to_date, item.State);
            
        var new_version = new Version("2.0.0.0");

        Assert.Equal
        (
            ms.FromManifest
            (
                manifest with {
                    Version = new_version
                }
            ),
            state with { Updated = false }
        );

        item.State = new NotInstalledState();

        await ms.RecordUninstall(item);
            
        Assert.Equal(ms.FromManifest(manifest), new NotInstalledState());
    }
}