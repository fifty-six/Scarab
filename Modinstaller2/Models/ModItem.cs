using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.IO.Compression;

namespace Modinstaller2.Models
{
    public class ModItem : INotifyPropertyChanged
    {
        internal bool? _enabled;
        internal bool _installed;
        internal string[] _files;
        internal string _link;

        public string Name { get; set; }

        public bool? Enabled
        {
            get => _enabled;

            set
            {
                _enabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            }
        }

        public bool Installed
        {
            get => _installed;

            set
            {
                _installed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Installed)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnEnable()
        {
            if (!(Enabled is bool enabled)) throw new NotImplementedException();

            foreach (string file in _files)
            {
                string disabledPath = Path.Combine(InstallerSettings.Instance.ModsFolder, "Disabled", file);
                string enabledPath = Path.Combine(InstallerSettings.Instance.ModsFolder, file);

                // The variable just changed so this logic looks weird, but it's right.
                if (!enabled)
                {
                    if (File.Exists(disabledPath))
                        File.Delete(disabledPath);

                    File.Move(enabledPath, disabledPath);
                }
                else
                {
                    if (File.Exists(enabledPath))
                        throw new Exception("File already exists!");

                    File.Move(disabledPath, enabledPath);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Enabled: {Enabled}, Installed: {Installed}");
        }

        public void OnInstall()
        {
            // NOTE: Condition is taken *after* Installed is set to its new state.
            // So if it's not installed, it truly is not installed
            // And if it's "installed", we should install it.
            if (!Installed)
            {
                Uninstall();

                Enabled = null;
            }
            else
            {
                Install();

                Enabled = true;
            }
        }

        private void Install()
        {
            var dl = new WebClient();

            byte[] data = dl.DownloadData(new Uri(_link));

            string filename = string.Empty;

            if (!string.IsNullOrEmpty(dl.ResponseHeaders["Content-Disposition"]))
            {
                ContentDisposition disposition = new ContentDisposition(dl.ResponseHeaders["Content-Disposition"]);

                filename = disposition.FileName;
            }

            if (string.IsNullOrEmpty(filename))
                throw new Exception($"Filename unknown for Mod {Name}!");

            if (filename.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
            {
                using ZipArchive archive = new ZipArchive(new MemoryStream(data));

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("dll"))
                    {
                        entry.ExtractToFile(Path.Combine(InstallerSettings.Instance.ModsFolder, entry.Name), true);
                    }
                    else if (entry.Name.StartsWith("README"))
                    {
                        // Handle README
                    }
                }
            }
            else if (filename.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
            {
                File.WriteAllBytes(Path.Combine(InstallerSettings.Instance.ModsFolder, filename), data);
            }
            else
            {
                throw new NotImplementedException($"Unknown file type for mod download: {filename}");
            }
        }

        private void Uninstall()
        {
            foreach (string file in _files)
            {
                string path = Path.Combine(InstallerSettings.Instance.ModsFolder, file);

                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}