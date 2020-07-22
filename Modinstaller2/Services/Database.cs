using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
                List<string> files = modlink["Files"].ChildNodes.Cast<XmlNode>().Select(file => file["Name"].InnerText).ToList();

                var item = new ModItem
                {
                    Installed = files.All
                    (
                        f => paths.Select(path => Path.Join(path, f)).Any(File.Exists)
                    ),

                    Link = modlink["Link"].InnerText,

                    Files = files.ToArray(),

                    Name = modlink["Name"].InnerText,

                    Db = this,

                    Dependencies = modlink["Dependencies"].ChildNodes.Cast<XmlNode>().Select(x => x.InnerText).ToArray()
                };

                Debug.WriteLine($"{item.Name}: {string.Join(" ", item.Dependencies)}");

                item.Enabled = item.Installed ? (bool?) files.All
                (
                    f => enabled_paths.Select(path => Path.Join(path, f)).Any(File.Exists)
                ) 
                : null;

                _items.Add(item);
            }

            _items.Sort((a, b) => string.Compare(a.Name, b.Name));
        }
    }
}
