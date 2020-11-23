using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Xml.Serialization;
using Modinstaller2.Models;

namespace Modinstaller2.Services
{
    public class Database
    {
        public const string MODLINKS_URI = "https://raw.githubusercontent.com/Ayugradow/ModInstaller/master/modlinks.xml";

        public IEnumerable<ModItem> Items => _items;

        private readonly List<ModItem> _items = new List<ModItem>();

        private Database(ModLinks ml)
        {
            string[] enabled_paths =
            {
                InstallerSettings.Instance.ModsFolder,
                InstallerSettings.Instance.ManagedFolder,
            };

            string[] paths = enabled_paths.Append(InstallerSettings.Instance.DisabledFolder).ToArray();

            ModList list = ml.ModList;

            foreach (ModLink mod in list.ModLinks)
            {
                string[] files = mod.Files.Value.Select(x => x.Name).ToArray();

                Dictionary<string, string> hashes = mod.Files.Value.ToDictionary(file => file.Name, file => file.SHA1);

                var item = new ModItem
                {
                    Installed = files.All
                    (
                        f => paths.Select(path => Path.Join(path, f)).Any(File.Exists)
                    ),

                    Link = mod.Link,

                    Files = files,

                    Name = mod.Name,

                    Description = mod.Description ?? "This mod has no description.",

                    Dependencies = mod.Dependencies.String.ToArray(),
                };

                item.Updated = item.Installed ? CheckFileHashes(files, paths, hashes) : (bool?) null;


                item.Enabled = item.Installed
                    ? (bool?) files.All(f => enabled_paths.Select(path => Path.Join(path, f)).Any(File.Exists))
                    : null;

                _items.Add(item);
            }

            _items.Sort((a, b) => string.Compare(a.Name, b.Name));
        }

        public static Database FromUrl(string uri)
        {
            using var wc = new WebClient();

            string xmlString = wc.DownloadString(new Uri(uri));

            var serializer = new XmlSerializer(typeof(ModLinks));

            using TextReader reader = new StringReader(xmlString);

            var ml = (ModLinks) serializer.Deserialize(reader);

            return new Database(ml);
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
