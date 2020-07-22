using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mime;
using Modinstaller2.Services;

namespace Modinstaller2.Models
{
    public class ModItem : INotifyPropertyChanged
    {
        internal bool? _enabled;

        internal bool _installed;

        public string[] Dependencies { get; set; }

        public string[] Files { get; set; }

        public string Link { get; set; }

        public Database Db { get; set; }

        public string Name { get; set; }

        private static readonly string[] BLACKLIST =
        {
            "hollow_Knight_Data/",
            "Managed/",
            "Mods/"
        };

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

            foreach (string file in Files)
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

            Debug.WriteLine($"Enabled: {Enabled}, Installed: {Installed}");
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
            foreach (ModItem dep in Dependencies.Select(x => Db.Items.FirstOrDefault(i => i.Name == x)).Where(x => x != null))
            {
                if (dep.Installed) 
                    continue;

                dep.Install();

                dep.Installed = true;
                dep.Enabled = true;
            }
            
            var dl = new WebClient();

            byte[] data = dl.DownloadData(new Uri(Link));

            string filename = string.Empty;

            if (!string.IsNullOrEmpty(dl.ResponseHeaders["Content-Disposition"]))
            {
                var disposition = new ContentDisposition(dl.ResponseHeaders["Content-Disposition"]);

                filename = disposition.FileName;
            }

            if (string.IsNullOrEmpty(filename))
                throw new Exception($"Filename unknown for Mod {Name}!");

            string ext = Path.GetExtension(filename.ToLower());

            var settings = InstallerSettings.Instance;
            
            switch (ext)
            {
                case ".zip":
                {
                    using var archive = new ZipArchive(new MemoryStream(data));

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("dll"))
                        {
                            entry.ExtractToFile
                            (
                                entry.Name.StartsWith
                                    ("Assembly-CSharp")
                                    ? Path.Combine(settings.ManagedFolder, entry.Name)
                                    : Path.Combine(settings.ModsFolder, entry.Name),
                                true
                            );
                        }
                        else if (entry.Name.StartsWith("README"))
                        {
                            // TODO: Handle README
                        }
                        // Folder, ignore if it's one of the base direcetories.
                        else if (string.IsNullOrEmpty(entry.Name) && BLACKLIST.All(i => !entry.FullName.EndsWith(i, StringComparison.OrdinalIgnoreCase)))
                        {
                            // If we're using the full zip structure, remove the prev directories.
                            string dirPath = entry.FullName.Replace("hollow_knight_Data/Managed/Mods/", "");

                            Directory.CreateDirectory(Path.Combine(settings.ModsFolder, dirPath));
                        }
                        // Any other file, we place it according to the normal structure
                        else if (entry.Name != string.Empty)
                        {
                            // Once again, ignore the first few folders of zip structure.
                            string path = entry.FullName.Replace("hollow_knight_Data/Managed/Mods/", "");

                            entry.ExtractToFile(Path.Combine(settings.ModsFolder, path), true);
                        }
                    }

                    break;
                }

                case ".dll":
                {
                    File.WriteAllBytes(Path.Combine(settings.ModsFolder, filename), data);

                    break;
                }

                default:
                {
                    throw new NotImplementedException($"Unknown file type for mod download: {filename}");
                }
            }
        }

        private void Uninstall()
        {
            foreach (string file in Files)
            {
                string path = Path.Combine
                (
                    Enabled ?? true 
                        ? InstallerSettings.Instance.ModsFolder
                        : InstallerSettings.Instance.DisabledFolder,
                    file
                );

                if (File.Exists(path))
                    File.Delete(path);
            }

            foreach (ModItem dep in Dependencies.Select(x => Db.Items.FirstOrDefault(i => x == i.Name)).Where(x => x != null))
            {
                // Make sure no other mods depend on it
                if (Db.Items.Where(x => x.Installed && x != this).Any(x => x.Dependencies.Contains(dep.Name)))
                    continue;

                dep.Uninstall();

                dep.Installed = false;
                dep.Enabled = null;
            }
        }
    }
}