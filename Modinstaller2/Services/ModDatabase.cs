using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Xml.Serialization;
using Modinstaller2.Models;

namespace Modinstaller2.Services
{
    public class ModDatabase
    {
        private const string MODLINKS_URI = "https://raw.githubusercontent.com/hk-modding/modlinks/main/ModLinks.xml";

        public IEnumerable<ModItem> Items => _items;

        private readonly List<ModItem> _items = new();

        private ModDatabase(InstalledMods mods, ModLinks ml, Settings config)
        {
            foreach (var mod in ml.Manifests)
            {
                var item = new ModItem
                (
                    link: Environment.OSVersion.Platform switch
                    {
                        PlatformID.Win32NT => mod.Links.Windows.URL,
                        PlatformID.MacOSX => mod.Links.Mac.URL,
                        PlatformID.Unix => mod.Links.Linux.URL,

                        var val => throw new NotSupportedException(val.ToString())
                    },

                    version: mod.Version.Value,
                    name: mod.Name,
                    description: mod.Description,
                    dependencies: mod.Dependencies,
                    config: config,
                    
                    state: mods.FromManifest(mod)
                );
                
                _items.Add(item);
            }

            _items.Sort((a, b) => string.Compare(a.Name, b.Name));
        }

        public ModDatabase(InstalledMods mods, Settings config) : this(mods, GetModLinks(), config) { }

        private static ModLinks GetModLinks()
        {
            var uri = new Uri(MODLINKS_URI);

            using var wc = new WebClient
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate)
            };

            string xmlString = wc.DownloadString(uri);

            var serializer = new XmlSerializer(typeof(ModLinks));

            using TextReader reader = new StringReader(xmlString);

            var ml = (ModLinks?) serializer.Deserialize(reader);

            if (ml is null)
                throw new InvalidDataException();

            return ml;
        }
    }
}