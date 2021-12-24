using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Scarab.Interfaces;
using Scarab.Models;

namespace Scarab.Services
{
    public class ModDatabase : IModDatabase
    {
        private const string MODLINKS_URI = "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@latest/ModLinks.xml";
        private const string APILINKS_URI = "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@latest/ApiLinks.xml";

        public (string Url, int Version) Api { get; }

        public IEnumerable<ModItem> Items => _items;

        private readonly List<ModItem> _items = new();

        private ModDatabase(IModSource mods, ModLinks ml, ApiLinks al)
        {
            foreach (var mod in ml.Manifests)
            {
                var item = new ModItem
                (
                    link: mod.Links.GetOSUrl(),
                    version: mod.Version.Value,
                    name: mod.Name,
                    description: mod.Description,
                    dependencies: mod.Dependencies,
                    
                    state: mods.FromManifest(mod)
                );
                
                _items.Add(item);
            }

            _items.Sort((a, b) => string.Compare(a.Name, b.Name));

            Api = (al.Manifest.Links.GetOSUrl(), al.Manifest.Version);
        }

        public ModDatabase(IModSource mods, (ModLinks ml, ApiLinks al) links) : this(mods, links.ml, links.al) { }

        public ModDatabase(IModSource mods, string modlinks, string apilinks) : this(mods, FromString<ModLinks>(modlinks), FromString<ApiLinks>(apilinks)) { }
        
        public static async Task<(ModLinks, ApiLinks)> FetchContent()
        {
            using var hc = new HttpClient {
                DefaultRequestHeaders = {
                    CacheControl = new CacheControlHeaderValue {
                        NoCache = true,
                        MustRevalidate = true
                    }
                }
            };
            
            Task<ModLinks> ml = FetchModLinks(hc);
            Task<ApiLinks> al = FetchApiLinks(hc);

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
            var uri = new Uri(APILINKS_URI);

            return FromString<ApiLinks>(await hc.GetStringAsync(uri));
        }
        
        private static async Task<ModLinks> FetchModLinks(HttpClient hc)
        {
            var uri = new Uri(MODLINKS_URI);

            return FromString<ModLinks>(await hc.GetStringAsync(uri));
        }
    }
}