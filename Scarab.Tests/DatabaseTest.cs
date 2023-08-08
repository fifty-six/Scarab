using System;
using System.Collections.Immutable;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.Services;
using Xunit;

namespace Scarab.Tests;

public class DatabaseTest
{
    private static readonly string modlinks_xml = @"
            <?xml version=""1.0""?>
            <ModLinks
                xmlns=""https://github.com/HollowKnight-Modding/HollowKnight.ModLinks/HollowKnight.ModManager""
                xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
                xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
                xsi:schemaLocation=""https://raw.githubusercontent.com/HollowKnight-Modding/HollowKnight.ModLinks/main/Schemas/ModLinks.xml""
            >
                <Manifest>
                    <Name>QoL</Name>
                    <Description>A collection of various quality of life improvements.</Description>
                    <Version>3.0.0.0</Version>
                    
                    <Link SHA256=""766555BF9E8F784EB00CEE3AF0A3991E3E3E4BDD73E4816CEDD82840FF432E18"">
                        <![CDATA[https://github.com/fifty-six/HollowKnight.QoL/releases/download/v3/QoL.zip]]>
                    </Link>
                    
                    <Dependencies>
                        <Dependency>Vasi</Dependency>
                    </Dependencies>

                    <Repository>
                        <![CDATA[https://github.com/fifty-six/HollowKnight.QoL/]]>
                    </Repository>
                </Manifest>
                <Manifest>
                    <Name>Vasi</Name>
                    <Description>A library with some utility classes.</Description>
                    <Version>2.0.0.0</Version>
                    
                    <Link SHA256=""B93FA7ECDF40D5F91F942ACFD31CD2A5243551720C96E18DDE99FD64919162EC"">
                        <![CDATA[https://github.com/fifty-six/HollowKnight.Vasi/releases/download/v2/Vasi.zip]]>
                    </Link>
                    
                    <Dependencies />

                    <Repository>
                        <![CDATA[https://github.com/fifty-six/HollowKnight.Vasi/]]>
                    </Repository>
                </Manifest>
            </ModLinks>
        ".Trim();

    private static readonly string api_xml = @"
            <?xml version=""1.0""?>
            <ApiLinks
                xmlns=""https://github.com/HollowKnight-Modding/HollowKnight.ModLinks/HollowKnight.ModManager""
                xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
                xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
                xsi:schemaLocation=""https://raw.githubusercontent.com/HollowKnight-Modding/HollowKnight.ModLinks/main/Schemas/ApiLinks.xml""
            >
                <Manifest>
                    <Version>63</Version>
            
                    <Links>
                        <Linux SHA256=""21A0BB816283C4C90FCC944A19F5098D943E1B9EC5C6BA35DDBB27BCA6C43F95"">
                            <![CDATA[https://github.com/hk-modding/api/releases/download/1.5.75.11827-63/ModdingApiUnix.zip]]>
                        </Linux>
                        <Mac SHA256=""21A0BB816283C4C90FCC944A19F5098D943E1B9EC5C6BA35DDBB27BCA6C43F95"">
                            <![CDATA[https://github.com/hk-modding/api/releases/download/1.5.75.11827-63/ModdingApiUnix.zip]]>
                        </Mac>
                        <Windows SHA256=""7214D6D1DC144D7AAB7C1B9679546EDCCFFA650384FE07AAD1FA93DD72A17E10"">
                            <![CDATA[https://github.com/hk-modding/api/releases/download/1.5.75.11827-63/ModdingApiWin.zip]]>
                        </Windows>
                    </Links>
                    
                    <Files>
                        <File>Assembly-CSharp.dll</File>
                        <File>Assembly-CSharp.xml</File>
                        
                        <File>MMHOOK_Assembly-CSharp.dll</File>
                        <File>MMHOOK_PlayMaker.dll</File>
                        
                        <File>Mono.Cecil.dll</File>
                        <File>MonoMod.RuntimeDetour.dll</File>
                        <File>MonoMod.Utils.dll</File>
                        
                        <File>mscorlib.dll</File>
                        
                        <File>Newtonsoft.Json.dll</File>
                    </Files>
                </Manifest>
            </ApiLinks>
        ".Trim();

    [Fact]
    public void Serialization()
    {
        IModSource src = new InstalledMods(new MockFileSystem());

        IModDatabase db = new ModDatabase(src, modlinks_xml, api_xml);

        Assert.Equal
        (
            new ModItem
            (
                new NotInstalledState(),
                new Version(3, 0, 0, 0),
                new[] { "Vasi" },
                "https://github.com/fifty-six/HollowKnight.QoL/releases/download/v3/QoL.zip",
                string.Empty,
                "QoL",
                "A collection of various quality of life improvements.",
                "https://github.com/fifty-six/HollowKnight.QoL",
                ImmutableArray<Tag>.Empty, 
                Array.Empty<string>(),
                Array.Empty<string>()
            ),
            db.Items.First(x => x.Name == "QoL")
        );
            
        Assert.Equal(
            63,
            db.Api.Version
        );
    }

    [Fact]
    public void ReadEmptyState()
    {
        IModSource src = new InstalledMods(new MockFileSystem());

        IModDatabase db = new ModDatabase(src, modlinks_xml, api_xml);

        Assert.True(db.Items.All(x => x.State is NotInstalledState));
    }

    [Fact]
    public void ReadState()
    {
        var src = new InstalledMods(new MockFileSystem())
        {
            Mods =
            {
                ["QoL"] = new InstalledState(true, new Version(1, 0), false)
            }
        };

        IModDatabase db = new ModDatabase(src, modlinks_xml, api_xml);

        Assert.True(db.Items.First(x => x.Name == "QoL").State is InstalledState { Updated: false, Enabled: true });
        Assert.False(db.Items.First(x => x.Name == "Vasi").Installed);
    }
}