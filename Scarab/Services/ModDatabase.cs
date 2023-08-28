using System.Xml.Serialization;

namespace Scarab.Services;

public class ModDatabase : IModDatabase
{
    private const string MODLINKS_URI = "https://raw.githubusercontent.com/hk-modding/modlinks/main/ModLinks.xml";
    private const string APILINKS_URI = "https://raw.githubusercontent.com/hk-modding/modlinks/main/ApiLinks.xml";
        
    private const string FALLBACK_MODLINKS_URI = "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@latest/ModLinks.xml";
    private const string FALLBACK_APILINKS_URI = "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@latest/ApiLinks.xml";

    public (string Url, int Version, string SHA256) Api { get; }

    public IEnumerable<ModItem> Items => _items;

    private readonly List<ModItem> _items = new();

    private ModDatabase(IModSource mods, ModLinks ml, ApiLinks al)
    {
        foreach (var mod in ml.Manifests)
        {
            var tags = mod.Tags.Select(x => Enum.TryParse(x, out Tag tag) ? (Tag?) tag : null)
                          .OfType<Tag>()
                          .ToImmutableArray();
                
            var item = new ModItem
            (
                link: mod.Links.OSUrl,
                version: mod.Version.Value,
                name: mod.Name,
                shasum: mod.Links.SHA256,
                description: mod.Description,
                repository: mod.Repository,
                dependencies: mod.Dependencies,
                    
                tags: tags,
                integrations: mod.Integrations,
                authors: mod.Authors,
                    
                state: mods.FromManifest(mod)
                    
            );
                
            _items.Add(item);
        }

        _items.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        Api = (al.Manifest.Links.OSUrl, al.Manifest.Version, al.Manifest.Links.SHA256);
    }

    public ModDatabase(IModSource mods, (ModLinks ml, ApiLinks al) links) : this(mods, links.ml, links.al) { }

    public ModDatabase(IModSource mods, string modlinks, string apilinks) : this(mods, FromString<ModLinks>(modlinks), FromString<ApiLinks>(apilinks)) { }
        
    public static async Task<(ModLinks, ApiLinks)> FetchContent(HttpClient hc)
    {
        var ml = FetchModLinks(hc);
        var al = FetchApiLinks(hc);

        await Task.WhenAll(ml, al);

        return (await ml, await al);
    }
        
    private static T FromString<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
            
        using TextReader reader = new StringReader(xml);

        var obj = (T?) serializer.Deserialize(reader);

        if (obj is null)
            throw new InvalidDataException();

        return obj;
    }

    private static async Task<ApiLinks> FetchApiLinks(HttpClient hc)
    {
        return FromString<ApiLinks>(await FetchWithFallback(hc, new Uri(APILINKS_URI), new Uri(FALLBACK_APILINKS_URI)));
    }
        
    private static async Task<ModLinks> FetchModLinks(HttpClient hc)
    {
        return FromString<ModLinks>(await FetchWithFallback(hc, new Uri(MODLINKS_URI), new Uri(FALLBACK_MODLINKS_URI)));
    }

    private static async Task<string> FetchWithFallback(HttpClient hc, Uri uri, Uri fallback)
    {
        try
        {
            var cts = new CancellationTokenSource(3000);
            return await hc.GetStringAsync(uri, cts.Token);
        }
        catch (Exception e) when (e is TaskCanceledException or HttpRequestException)
        {
            var cts = new CancellationTokenSource(3000);
            return await hc.GetStringAsync(fallback, cts.Token);
        }
    }
}