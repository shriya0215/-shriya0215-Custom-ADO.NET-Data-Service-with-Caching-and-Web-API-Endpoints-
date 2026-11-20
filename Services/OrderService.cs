using DataService.Data;
using DataService.Models;

namespace DataService.Services;

public interface IOrderService
{
    Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct);
    Task<Order?> GetOrderAsync(int id, CancellationToken ct);
}

/// <summary>Places orders and evicts cached product data whose stock just changed.</summary>
public sealed class OrderService(IOrderRepository repository, ProductCache cache) : IOrderService
{
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        var order = await repository.PlaceOrderAsync(request, ct);

        // Only after a successful commit: a rolled-back order changed nothing, so the cache stays valid.
        foreach (var productId in request.Items.Select(i => i.ProductId).Distinct())
        {
            cache.InvalidateProduct(productId);
        }

        cache.InvalidateCatalog();
        return order;
    }

    public Task<Order?> GetOrderAsync(int id, CancellationToken ct) => repository.GetByIdAsync(id, ct);
}
