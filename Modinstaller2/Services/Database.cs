using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Security.Cryptography;
using System.Xml.Serialization;
using Modinstaller2.Models;

namespace Modinstaller2.Services
{
    public class Database
    {
        public const string MODLINKS_URI = "https://raw.githubusercontent.com/Ayugradow/ModInstaller/master/modlinks.xml";

        public IEnumerable<ModItem> Items => _items;

        private readonly List<ModItem> _items = new();

        private Database(ModLinks ml, Settings config)
        {
            string[] enabled_paths =
            {
                config.ModsFolder,
                config.ManagedFolder,
            };

            string[] paths = enabled_paths.Append(config.DisabledFolder).ToArray();

            ModList list = ml.ModList;

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (ModLink mod in list.ModLinks)
            {
                string[] files = mod.Files.Value.Select(x => x.Name).ToArray();

                Dictionary<string, string> hashes = mod.Files.Value.ToDictionary(file => file.Name, file => file.SHA1);

                var item = new ModItem
                {
                    Link = mod.Link,
                    
                    Files = files,
                    
                    Name = mod.Name,
                    
                    Description = mod.Description ?? "This mod has no description.",
                    
                    Dependencies = mod.Dependencies.String.ToArray(),
                    
                    Config = config,
                    
                    State = files.All(f => paths.Select(path => Path.Join(path, f)).Any(File.Exists))
                        ? new InstalledMod
                        (
                            Updated: CheckFileHashes(files, paths, hashes),
                            Enabled: CheckEnabled(files, enabled_paths)
                        )
                        : new NotInstalledMod()
                };

                _items.Add(item);
            }

            _items.Sort((a, b) => string.Compare(a.Name, b.Name));
        }

        private static bool CheckEnabled(IEnumerable<string> files, string[] enabledPaths)
        {
            return files.All(f => enabledPaths.Select(path => Path.Join(path, f)).Any(File.Exists));
        }

        public static Database FromConfig(Settings config)
        {
            string xmlString;
            string uri = config.Modlinks;
            if (uri.StartsWith("file://"))
            {
                string path = uri.Substring(7);
                xmlString = File.ReadAllText(path);
            } else {

            using var wc = new WebClient
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate)
            };

                xmlString = wc.DownloadString(new Uri(uri));
            }

            var serializer = new XmlSerializer(typeof(ModLinks));

            using TextReader reader = new StringReader(xmlString);

            var ml = (ModLinks) serializer.Deserialize(reader);

            return new Database(ml, config);
        }

        private static string GetHash(string path)
        {
            using var sha1 = SHA1.Create();
            using FileStream stream = File.OpenRead(path);

            byte[] hashBytes = sha1.ComputeHash(stream);

            string f_hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);

            return f_hash;
        }

        private static bool CheckFileHashes(IEnumerable<string> files, string[] paths, IReadOnlyDictionary<string, string> hashes)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (string file in files)
            {
                string path = paths.Select(p => Path.Join(p, file)).First(File.Exists);

                if (GetHash(path) != hashes[file].ToUpper())
                    return false;
            }
            
            return true;
        }
    }
}
