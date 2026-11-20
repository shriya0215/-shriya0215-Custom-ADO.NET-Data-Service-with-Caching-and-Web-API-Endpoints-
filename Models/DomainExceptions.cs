namespace DataService.Models;

/// <summary>Product is missing or does not have enough stock for the requested quantity.</summary>
public sealed class InsufficientStockException(int productId)
    : Exception($"Product {productId} does not exist or has insufficient stock.")
{
    public int ProductId { get; } = productId;
}

/// <summary>Product cannot be deleted because existing orders reference it.</summary>
public sealed class ProductInUseException(int productId)
    : Exception($"Product {productId} is referenced by existing orders and cannot be deleted.")
{
    public int ProductId { get; } = productId;
}
