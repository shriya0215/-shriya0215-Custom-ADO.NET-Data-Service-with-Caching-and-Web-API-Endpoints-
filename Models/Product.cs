using System.ComponentModel.DataAnnotations;

namespace DataService.Models;

/// <summary>A product as returned to API clients.</summary>
public sealed record Product(int Id, string Name, string Category, decimal Price, int Stock);

/// <summary>Payload for creating or replacing a product.</summary>
public sealed class ProductInput
{
    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string Category { get; init; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int Stock { get; init; }
}

public sealed record CategorySummary(string Category, int ProductCount, decimal AveragePrice);

/// <summary>Read-model built from a disconnected DataSet (two tables).</summary>
public sealed record Catalog(IReadOnlyList<Product> Products, IReadOnlyList<CategorySummary> Summary);
