using System.Collections.Generic;
using System.Xml.Serialization;

namespace Modinstaller2.Models
{
    [XmlRoot(ElementName = "Installer")]
    public class Installer
    {
        [XmlElement(ElementName = "Link")]
        public string Link { get; set; }

        [XmlElement(ElementName = "SHA1")]
        public string SHA1 { get; set; }

        [XmlElement(ElementName = "AULink")]
        public string AULink { get; set; }
    }

    [XmlRoot(ElementName = "File")]
    public class ModFile
    {
        [XmlElement(ElementName = "Name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "SHA1")]
        public string SHA1 { get; set; }

        [XmlElement(ElementName = "Patch")]
        public string Patch { get; set; }
    }

    [XmlRoot(ElementName = "Files")]
    public class Files
    {
        [XmlElement(ElementName = "File")]
        public List<ModFile> Value { get; set; }
    }

    [XmlRoot(ElementName = "ModLink")]
    public class ModLink
    {
        [XmlElement(ElementName = "Name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "Files")]
        public Files Files { get; set; }

        [XmlElement(ElementName = "Link")]
        public string Link { get; set; }

        [XmlElement(ElementName = "UnixLink")]
        public string UnixLink { get; set; }

        [XmlElement(ElementName = "Dependencies")]
        public Dependencies Dependencies { get; set; }
        
        [XmlElement(ElementName = "Description")]
        public string Description { get; set; }
        
        [XmlElement(ElementName = "Optional")]
        public Optional Optional { get; set; }
    }

    [XmlRoot(ElementName = "Dependencies")]
    public class Dependencies
    {
        [XmlElement(ElementName = "string")]
        public List<string> String { get; set; }
    }

    [XmlRoot(ElementName = "Optional")]
    public class Optional
    {
        [XmlElement(ElementName = "string")]
        public string String { get; set; }
    }

    [XmlRoot(ElementName = "ModList")]
    public class ModList
    {
        [XmlElement(ElementName = "ModLink")]
        public List<ModLink> ModLinks { get; set; }
    }

    [XmlRoot(ElementName = "ModLinks")]
    public class ModLinks
    {
        [XmlElement(ElementName = "DriveLink")]
        public string DriveLink { get; set; }

        [XmlElement(ElementName = "Installer")]
        public Installer Installer { get; set; }

        [XmlElement(ElementName = "ModList")]
        public ModList ModList { get; set; }
    }
}