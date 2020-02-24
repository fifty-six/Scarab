using Modinstaller2.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;

namespace Modinstaller2.Services
{
    public class Database
    {
        private const string MODLINKS_URI = "https://raw.githubusercontent.com/Ayugradow/ModInstaller/master/modlinks.xml";

        public IEnumerable<ModItem> GetItems() => _items;

        private readonly List<ModItem> _items = new List<ModItem>();

        public Database()
        {
            using var wc = new WebClient();

            string xmlString = wc.DownloadString(new Uri(MODLINKS_URI));

            XmlDocument doc = new XmlDocument();

            doc.LoadXml(xmlString);

            XmlElement list = doc["ModLinks"]["ModList"];

            foreach (XmlNode modlink in list.ChildNodes)
            {
                ModItem item = new ModItem();

                List<string> files = new List<string>();

                foreach (XmlNode file in modlink["Files"].ChildNodes)
                {
                    files.Add(file["Name"].InnerText);
                }

#warning TODO: Set this up to initialize after the settings. Probably in MainWindowViewModel.
                item.Installed = false; /* Enumerable.All
                (
                    files, (f) => File.Exists(Path.Combine(InstallerSettings.Instance.ModsFolder, f))
                               || File.Exists(Path.Combine(InstallerSettings.Instance.DisabledFolder, f))
                ); */

                item.Enabled = false; /* item.Installed ? (bool?) Enumerable.All
                (
                    files, (f) => File.Exists(Path.Combine(InstallerSettings.Instance.ModsFolder, f))
                ) 
                : null; */

                item._link = modlink["Link"].InnerText;

                item._files = files.ToArray();

                item.Name = modlink["Name"].InnerText;

                _items.Add(item);
            }

            _items.Sort((a, b) => string.Compare(a.Name, b.Name));
        }
    }
}
