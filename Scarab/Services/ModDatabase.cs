using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Scarab.Interfaces;
using Scarab.Models;

namespace Scarab.Services
{
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
                var item = new ModItem
                (
                    link: mod.Links.OSUrl,
                    version: mod.Version.Value,
                    name: mod.Name,
                    shasum: mod.Links.SHA256,
                    description: mod.Description,
                    repository: mod.Repository,
                    dependencies: mod.Dependencies,
                    
                    state: mods.FromManifest(mod)
                );
                
                _items.Add(item);
            }

            _items.Sort((a, b) => string.Compare(a.Name, b.Name));

            Api = (al.Manifest.Links.OSUrl, al.Manifest.Version, al.Manifest.Links.SHA256);
        }

        public ModDatabase(IModSource mods, (ModLinks ml, ApiLinks al) links) : this(mods, links.ml, links.al) { }

        public ModDatabase(IModSource mods, string modlinks, string apilinks) : this(mods, FromString<ModLinks>(modlinks), FromString<ApiLinks>(apilinks)) { }
        
        public static async Task<(ModLinks, ApiLinks)> FetchContent()
        {
            async Task<(ModLinks, ApiLinks)> TryFetch(HttpClient hc)
            {
                var ml = FetchModLinks(hc);
                var al = FetchApiLinks(hc);

                return (await ml, await al);
            }

            HttpClient AddSettings(HttpClient hc)
            {
                hc.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    MustRevalidate = true
                };
                
                hc.DefaultRequestHeaders.Add("User-Agent", "Scarab");

                return hc;
            }

            using var hc = AddSettings(new HttpClient());
            
            hc.DefaultRequestHeaders.Add("User-Agent", "Scarab");

            try
            {
                return await TryFetch(hc);
            }
            catch (TaskCanceledException)
            {
                Trace.WriteLine("Normal HttpClient timed out, trying work-around client.");
                
                using HttpClient workaround = AddSettings(CreateWorkaroundClient());

                return await TryFetch(hc);
            }
        }
        
        // .NET has a thing with using IPv6 for IPv4 stuff, so on
        // networks and/or drivers w/ poor support this fails.
        // https://github.com/dotnet/runtime/issues/47267
        // https://github.com/fifty-six/Scarab/issues/47
        private static HttpClient CreateWorkaroundClient()
        {
            SocketsHttpHandler handler = new SocketsHttpHandler
            {
                ConnectCallback = IPv4ConnectAsync
            };
            return new HttpClient(handler);
        
            static async ValueTask<Stream> IPv4ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
            {
                // By default, we create dual-mode sockets:
                // Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        
                // Defaults to dual-mode sockets, which uses IPv6 for IPv4 stuff.
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = true;
        
                try
                {
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
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
}