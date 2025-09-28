namespace Scarab.Mock;

public class MockDatabase : IModDatabase
{
    private static Links _EmptyLink = Links.FromSingle("link", "sha");
    
    public IEnumerable<ModItem> Items { get; } = new ModItem[]
    {
        // Installed and up to date
        new
        (
            new InstalledState(true, new Version(1, 0), true),
            new Version(1, 0),
            new[] { "ILove", "Having", "Dependencies" },
            _EmptyLink,
            "NormalEx",
            "An example",
            "https://github.com/fifty-six/HollowKnight.QoL",
            ImmutableArray.Create(Tag.Boss, Tag.Utility),
            new[] { "ILove", "Having", "Integrations" },
            new[] { "56", "57", "58" }
        ),
        // Installed but out of date
        new
        (
            new InstalledState(true, new Version(1, 0), false),
            new Version(2, 0),
            Array.Empty<string>(),
            _EmptyLink,
            "OutOfDateEx",
            "An example",
            "https://github.com/fifty-six/yup",
            ImmutableArray.Create(Tag.Library),
            Array.Empty<string>(),
            Array.Empty<string>()
        ),
        // Not installed
        new
        (
            new NotInstalledState(),
            new Version(1, 0),
            Array.Empty<string>(),
            _EmptyLink,
            "NotInstalledEx",
            "An example",
            "example.com",
            ImmutableArray<Tag>.Empty,
            Array.Empty<string>(),
            Array.Empty<string>()
        ),
        // Example with a really long name and tags and integrations
        new
        (
            new NotInstalledState(),
            new Version(1, 0),
            Array.Empty<string>(),
            _EmptyLink,
            string.Join("", Enumerable.Repeat("Very", 8)) + "LongModName",
            "An example",
            "https://example.com/really/really/really/really/really/long/url/to/test/wrapping/impls/....",
            ImmutableArray.Create(Tag.Cosmetic, Tag.Expansion, Tag.Gameplay),
            new[] { "NormalEx" },
            Array.Empty<string>()
        )
    };

    public (Links Link, int Version) Api { get; } = (_EmptyLink, 256);
}