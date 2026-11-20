using System.ComponentModel.DataAnnotations;

namespace DataService.Models;

public sealed class PlaceOrderRequest
{
    [Required, StringLength(100)]
    public string CustomerName { get; init; } = string.Empty;

    [Required, MinLength(1), MaxLength(50)]
    public List<OrderLineRequest> Items { get; init; } = new();
}

public sealed class OrderLineRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; init; }

    [Range(1, 1000)]
    public int Quantity { get; init; }
}

public sealed record OrderLine(int ProductId, int Quantity, decimal UnitPrice);

public sealed record Order(int Id, string CustomerName, DateTime CreatedUtc, decimal Total, IReadOnlyList<OrderLine> Items);
