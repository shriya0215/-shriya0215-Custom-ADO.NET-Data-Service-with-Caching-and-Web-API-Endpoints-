using System.ComponentModel.DataAnnotations;
using DataService.Models;
using DataService.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataService.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    /// <summary>GET /api/products?category=Office — cached, DataSet-backed list plus per-category summary.</summary>
    [HttpGet]
    public async Task<ActionResult<Catalog>> GetCatalog(
        [FromQuery, StringLength(50)] string? category, CancellationToken ct)
    {
        var result = await products.GetCatalogAsync(category, ct);
        AddCacheHeader(result.FromCache);
        return Ok(result.Value);
    }

    /// <summary>GET /api/products/5 — cached single product.</summary>
    [HttpGet("{id:int}", Name = nameof(GetProduct))]
    public async Task<ActionResult<Product>> GetProduct(int id, CancellationToken ct)
    {
        var result = await products.GetProductAsync(id, ct);
        AddCacheHeader(result.FromCache);
        return result.Value is null ? NotFound() : Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(ProductInput input, CancellationToken ct)
    {
        var product = await products.CreateAsync(input, ct);
        return CreatedAtRoute(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductInput input, CancellationToken ct) =>
        await products.UpdateAsync(id, input, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            return await products.DeleteAsync(id, ct) ? NoContent() : NotFound();
        }
        catch (ProductInUseException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Product cannot be deleted",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    // Makes caching visible to clients and to anyone testing the assignment.
    private void AddCacheHeader(bool fromCache) => Response.Headers["X-Cache"] = fromCache ? "HIT" : "MISS";
}
