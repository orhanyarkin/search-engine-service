using System.Xml.Serialization;

namespace SearchEngine.Infrastructure.Providers.Models;

/// <summary>Sağlayıcı 2 XML akış kök elemanı.</summary>
[XmlRoot("feed")]
public class Provider2Feed
{
    [XmlArray("items")]
    [XmlArrayItem("item")]
    public List<Provider2Item> Items { get; set; } = [];

    [XmlElement("meta")]
    public Provider2Meta Meta { get; set; } = null!;
}

/// <summary>Sağlayıcı 2 XML akışından gelen tekil öğe.</summary>
public class Provider2Item
{
    [XmlElement("id")]
    public string Id { get; set; } = string.Empty;

    [XmlElement("headline")]
    public string Headline { get; set; } = string.Empty;

    [XmlElement("type")]
    public string Type { get; set; } = string.Empty;

    [XmlElement("stats")]
    public Provider2Stats Stats { get; set; } = null!;

    [XmlElement("publication_date")]
    public string PublicationDate { get; set; } = string.Empty;

    [XmlArray("categories")]
    [XmlArrayItem("category")]
    public List<string> Categories { get; set; } = [];
}

/// <summary>Video veya makale metriklerini içerebilen istatistik elemanı.</summary>
public class Provider2Stats
{
    // Video metrikleri
    [XmlElement("views")]
    public string? Views { get; set; }

    [XmlElement("likes")]
    public string? Likes { get; set; }

    [XmlElement("duration")]
    public string? Duration { get; set; }

    // Makale metrikleri
    [XmlElement("reading_time")]
    public string? ReadingTime { get; set; }

    [XmlElement("reactions")]
    public string? Reactions { get; set; }

    [XmlElement("comments")]
    public string? Comments { get; set; }
}

/// <summary>Sağlayıcı 2 sayfalama meta verileri.</summary>
public class Provider2Meta
{
    [XmlElement("total_count")]
    public int TotalCount { get; set; }

    [XmlElement("current_page")]
    public int CurrentPage { get; set; }

    [XmlElement("items_per_page")]
    public int ItemsPerPage { get; set; }
}
