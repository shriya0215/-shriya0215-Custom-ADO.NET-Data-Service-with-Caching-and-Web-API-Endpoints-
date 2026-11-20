using DataService.Models;
using DataService.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataService.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>POST /api/orders — transactional: order, order lines and stock updates commit together or not at all.</summary>
    [HttpPost]
    public async Task<ActionResult<Order>> Place(PlaceOrderRequest request, CancellationToken ct)
    {
        try
        {
            var order = await orders.PlaceOrderAsync(request, ct);
            return CreatedAtRoute(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Order rejected, nothing was changed",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpGet("{id:int}", Name = nameof(GetOrder))]
    public async Task<ActionResult<Order>> GetOrder(int id, CancellationToken ct)
    {
        var order = await orders.GetOrderAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }
}
