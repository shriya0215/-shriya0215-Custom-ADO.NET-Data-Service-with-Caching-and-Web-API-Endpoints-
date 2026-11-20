using System.ComponentModel.DataAnnotations;

namespace DataService.Services;

/// <summary>Bound from the "Cache" section of appsettings.json; validated at startup.</summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Absolute lifetime of a cached single product.</summary>
    [Range(1, 86_400)]
    public int ProductSeconds { get; set; } = 120;

    /// <summary>Absolute lifetime of a cached catalog (DataSet-backed list).</summary>
    [Range(1, 86_400)]
    public int CatalogSeconds { get; set; } = 60;

    /// <summary>Upper bound on cached entries so arbitrary query strings cannot exhaust memory.</summary>
    [Range(1, 100_000)]
    public int MaxEntries { get; set; } = 1_000;
}
