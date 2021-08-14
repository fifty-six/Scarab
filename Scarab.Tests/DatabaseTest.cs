using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Scarab.Interfaces;
using Scarab.Models;
using Scarab.Services;
using Xunit;

namespace Scarab.Tests
{
    public class DatabaseTest
    {
        private static readonly string xml = @"
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
                </Manifest>
                <Manifest>
                    <Name>Vasi</Name>
                    <Description>A library with some utility classes.</Description>
                    <Version>2.0.0.0</Version>
                    
                    <Link SHA256=""B93FA7ECDF40D5F91F942ACFD31CD2A5243551720C96E18DDE99FD64919162EC"">
                        <![CDATA[https://github.com/fifty-six/HollowKnight.Vasi/releases/download/v2/Vasi.zip]]>
                    </Link>
                    
                    <Dependencies />
                </Manifest>
            </ModLinks>
        ".Trim();

        [Fact]
        public void Serialization()
        {
            IModSource src = new InstalledMods(new MockFileSystem());

            IModDatabase db = new ModDatabase(src, xml);

            Assert.Equal
            (
                db.Items.First(x => x.Name == "QoL"),
                new ModItem
                (
                    new NotInstalledState(),
                    new Version(3, 0, 0, 0),
                    new[] { "Vasi" },
                    "https://github.com/fifty-six/HollowKnight.QoL/releases/download/v3/QoL.zip",
                    "QoL",
                    "A collection of various quality of life improvements."
                )
            );
        }

        [Fact]
        public void ReadEmptyState()
        {
            IModSource src = new InstalledMods(new MockFileSystem());

            IModDatabase db = new ModDatabase(src, xml);

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

            IModDatabase db = new ModDatabase(src, xml);

            Assert.True(db.Items.First(x => x.Name == "QoL").State is InstalledState { Updated: false, Enabled: true });
            Assert.False(db.Items.First(x => x.Name == "Vasi").Installed);
        }
    }
}