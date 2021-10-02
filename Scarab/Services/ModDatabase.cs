using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Scarab.Interfaces;
using Scarab.Models;

namespace Scarab.Services
{
    public class ModDatabase : IModDatabase
    {
        private const string MODLINKS_URI = "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@main/ModLinks.xml";
        private const string APILINKS_URI = "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@main/ApiLinks.xml";

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
            Task<ModLinks> ml = FetchModLinks();
            Task<ApiLinks> al = FetchApiLinks();

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

        private static async Task<ApiLinks> FetchApiLinks()
        {
            var uri = new Uri(APILINKS_URI);
            
            using var wc = new WebClient
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate)
            };

            string xmlString = await wc.DownloadStringTaskAsync(uri);

            return FromString<ApiLinks>(xmlString);
        }
        
        private static async Task<ModLinks> FetchModLinks()
        {
            var uri = new Uri(MODLINKS_URI);

            using var wc = new WebClient
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate)
            };

            string xmlString = await wc.DownloadStringTaskAsync(uri);

            return FromString<ModLinks>(xmlString);
        }
    }
}