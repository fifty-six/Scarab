using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mime;
using Avalonia.Media;

namespace Modinstaller2.Models
{
    public class ModItem : INotifyPropertyChanged
    {
        internal bool? _enabled;

        internal bool _installed;

        public string[] Dependencies { get; set; }

        public string[] Files { get; set; }

        public string Link { get; set; }

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

        public bool? InstalledAndUpdated
        {
            // Indeterminate state to indicate update required.
            get => Updated ?? true ? _installed : (bool?) null;

            set
            {
                // ReSharper disable once PossibleInvalidOperationException
                _installed = (bool) value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Installed)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstalledAndUpdated)));
            }
        }

        public Color Color => Color.Parse(Updated ?? true ? "#ff086f9e" : "#f49107");

        public bool Installed
        {
            get => _installed;

            set
            {
                _installed = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Installed)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstalledAndUpdated)));
            }
        }

        public string InstallText => Updated ?? true ? "Installed?" : "Out of date!";

        public bool? Updated { get; set; }

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
        }

        public void OnInstall(IList<ModItem> items)
        {
            // NOTE: Condition is taken *after* Installed is set to its new state.
            // So if it's not installed, it truly is not installed
            // And if it's "installed", we should install it.
            if (!Installed)
            {
                if (Updated is bool updated && !updated)
                {
                    Install(items);

                    Updated = true;

                    // Have to set it explicitly, because null toggles to false.
                    Installed = true;
                }
                else
                {
                    Uninstall(items);

                    Enabled = null;
                }
            }
            else
            {
                Enabled = true;

                Install(items);
            }
        }

        private void Install(IList<ModItem> items)
        {
            foreach (ModItem dep in Dependencies.Select(x => items.FirstOrDefault(i => i.Name == x)).Where(x => x != null))
            {
                if (dep.Installed) 
                    continue;

                dep.Install(items);

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
            {
                filename = Link.Substring(Link.LastIndexOf("/") + 1);
            }

            string ext = Path.GetExtension(filename.ToLower());

            var settings = InstallerSettings.Instance;

            // Default to enabling
            string mod_folder = Enabled ?? true
                ? settings.ModsFolder
                : settings.DisabledFolder;
            
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
                                    : Path.Combine(mod_folder, entry.Name),
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
                    File.WriteAllBytes(Path.Combine(mod_folder, filename), data);

                    break;
                }

                default:
                {
                    throw new NotImplementedException($"Unknown file type for mod download: {filename}");
                }
            }
        }

        private void Uninstall(IList<ModItem> items)
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

            foreach (ModItem dep in Dependencies.Select(x => items.FirstOrDefault(i => x == i.Name)).Where(x => x != null))
            {
                // Make sure no other mods depend on it
                if (items.Where(x => x.Installed && x != this).Any(x => x.Dependencies.Contains(dep.Name)))
                    continue;

                dep.Uninstall(items);

                dep.Installed = false;
                dep.Enabled = null;
            }
        }
    }
}