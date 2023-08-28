using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Scarab.Models;

public static class SerializationConstants
{
    public const string NAMESPACE = "https://github.com/HollowKnight-Modding/HollowKnight.ModLinks/HollowKnight.ModManager";
}

[Serializable]
public record Manifest
{
    // Internally handle the Link/Links either-or divide
    private Links? _links;
    private Link? _link;

    public VersionWrapper Version = null!;

    public string Name { get; set; } = null!;

    public string Repository { get; set; } = null!;

    [XmlElement]
    public Link? Link
    {
        get => throw new NotImplementedException("This is only for XML Serialization!");
        set => _link = value;
    }

    public Links Links
    {
        get =>
            _links ??= new Links
            {
                Windows = _link ?? throw new InvalidDataException(nameof(_link)),
                Mac = _link,
                Linux = _link
            };
        set => _links = value;
    }

    [XmlArray("Dependencies")]
    [XmlArrayItem("Dependency")]
    public string[] Dependencies { get; set; } = null!;

    public string Description { get; set; } = null!;

    [XmlArray(ElementName = "Tags")]
    [XmlArrayItem(ElementName = "Tag")]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [XmlArray(ElementName = "Integrations")]
    [XmlArrayItem(ElementName = "Integration")]
    public string[] Integrations { get; set; } = Array.Empty<string>();
        
    [XmlArray(ElementName = "Authors")]
    [XmlArrayItem(ElementName = "Author")]
    public string[] Authors { get; set; } = Array.Empty<string>();

    public override string ToString()
    {
        return "{\n"
               + $"\t{nameof(Version)}: {Version},\n"
               + $"\t{nameof(Name)}: {Name},\n"
               + $"\t{nameof(Links)}: {(object?) _link ?? Links},\n"
               + $"\t{nameof(Dependencies)}: {string.Join(", ", Dependencies)},\n"
               + $"\t{nameof(Authors)}: {string.Join(", ", Authors)},\n"
               + $"\t{nameof(Description)}: {Description}\n"
               + "}";
    }
}

[Serializable]
public record VersionWrapper : IXmlSerializable
{
    public Version Value { get; set; } = null!;

    public XmlSchema? GetSchema() => null;
    public void ReadXml(XmlReader reader) => Value = Version.Parse(reader.ReadElementContentAsString());
    public void WriteXml(XmlWriter writer) => writer.WriteString(Value.ToString());

    public static implicit operator VersionWrapper(Version v) => new() { Value = v };

    public override string ToString() => Value.ToString();
}

public class Links
{
    public Link Windows = null!;
    public Link Mac = null!;
    public Link Linux = null!;

    public override string ToString()
    {
        return "Links {"
               + $"\t{nameof(Windows)} = {Windows},\n"
               + $"\t{nameof(Mac)} = {Mac},\n"
               + $"\t{nameof(Linux)} = {Linux}\n"
               + "}";
    }

    public string SHA256 
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return Windows.SHA256;
            if (OperatingSystem.IsMacOS())
                return Mac.SHA256;
            if (OperatingSystem.IsLinux())
                return Linux.SHA256;

            throw new NotSupportedException(Environment.OSVersion.Platform.ToString());
        }
    }

    public string OSUrl
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return Windows.URL;
            if (OperatingSystem.IsMacOS())
                return Mac.URL;
            if (OperatingSystem.IsLinux())
                return Linux.URL;
                
            throw new NotSupportedException(Environment.OSVersion.Platform.ToString());
        }
    }
}

public class Link
{
    [XmlAttribute]
    public string SHA256 = null!;

    [XmlText]
    public string URL = null!;

    public override string ToString()
    {
        return $"[Link: {nameof(SHA256)} = {SHA256}, {nameof(URL)}: {URL}]";
    }
}

[Serializable]
public class ApiManifest
{
    public int Version { get; set; }

    [XmlArray("Files")]
    [XmlArrayItem("File")]
    public List<string> Files { get; set; }

    public Links Links { get; set; }

    // For serializer and nullability
    public ApiManifest()
    {
        Files = null!;
        Links = null!;
    }
}

[XmlRoot(Namespace = SerializationConstants.NAMESPACE)]
public class ApiLinks
{
    public ApiManifest Manifest { get; set; } = null!;
}

[XmlRoot(Namespace = SerializationConstants.NAMESPACE)]
public class ModLinks
{
    [XmlElement("Manifest")]
    public Manifest[] Manifests { get; set; } = null!;

    public override string ToString()
    {
        return "ModLinks {[\n"
               + string.Join("\n", Manifests.Select(x => x.ToString()))
               + "]}";
    }
}