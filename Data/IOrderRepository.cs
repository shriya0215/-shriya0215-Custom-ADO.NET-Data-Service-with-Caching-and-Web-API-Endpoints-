using DataService.Models;

namespace DataService.Data;

public interface IOrderRepository
{
    /// <summary>
    /// Atomically creates the order, its lines and decrements stock (three tables).
    /// Throws <see cref="InsufficientStockException"/> and rolls everything back if any line cannot be filled.
    /// </summary>
    Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default);

    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
}
