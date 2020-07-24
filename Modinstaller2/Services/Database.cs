using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Xml;
using Modinstaller2.Models;

namespace Modinstaller2.Services
{
    public class Database
    {
        private const string MODLINKS_URI = "https://raw.githubusercontent.com/Ayugradow/ModInstaller/master/modlinks.xml";

        public IEnumerable<ModItem> Items => _items;

        private readonly List<ModItem> _items = new List<ModItem>();

        public Database()
        {
            string[] enabled_paths = {
                InstallerSettings.Instance.ModsFolder,
                InstallerSettings.Instance.ManagedFolder,
            };
            
            string[] paths = enabled_paths.Append(InstallerSettings.Instance.DisabledFolder).ToArray();

            using var wc = new WebClient();

            string xmlString = wc.DownloadString(new Uri(MODLINKS_URI));

            var doc = new XmlDocument();

            doc.LoadXml(xmlString);

            XmlElement list = doc["ModLinks"]["ModList"];

            foreach (XmlNode modlink in list.ChildNodes)
            {
                string[] files = modlink["Files"].ChildNodes.Cast<XmlNode>().Select(file => file["Name"].InnerText).ToArray();

                Dictionary<string, string> hashes = modlink["Files"].ChildNodes.Cast<XmlNode>().ToDictionary(file => file["Name"].InnerText, file => file["SHA1"].InnerText);

                var item = new ModItem
                {
                    Installed = files.All
                    (
                        f => paths.Select(path => Path.Join(path, f)).Any(File.Exists)
                    ),

                    Link = modlink["Link"].InnerText,

                    Files = files,

                    Name = modlink["Name"].InnerText,

                    Dependencies = modlink["Dependencies"].ChildNodes.Cast<XmlNode>().Select(x => x.InnerText).ToArray(),
                };

                item.Updated = item.Installed ? CheckFileHashes(files, paths, hashes) : (bool?) null;


                item.Enabled = item.Installed ? (bool?) files.All
                (
                    f => enabled_paths.Select(path => Path.Join(path, f)).Any(File.Exists)
                ) 
                : null;

                _items.Add(item);
            }

            _items.Sort((a, b) => string.Compare(a.Name, b.Name));
        }

        public string GetHash(string path)
        {
                using var sha1 = SHA1.Create();
                using FileStream stream = File.OpenRead(path);

                byte[] hashBytes = sha1.ComputeHash(stream);

                string f_hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);

                return f_hash;
        }

        public bool CheckFileHashes(string[] files, string[] paths, Dictionary<string, string> hashes)
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
