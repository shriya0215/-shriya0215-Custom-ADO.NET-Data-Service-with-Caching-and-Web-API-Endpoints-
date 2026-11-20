using DataService.Data;
using DataService.Models;
using Microsoft.Extensions.Options;

namespace DataService.Services;

public interface IProductService
{
    Task<CacheResult<Product?>> GetProductAsync(int id, CancellationToken ct);
    Task<CacheResult<Catalog?>> GetCatalogAsync(string? category, CancellationToken ct);
    Task<Product> CreateAsync(ProductInput input, CancellationToken ct);
    Task<bool> UpdateAsync(int id, ProductInput input, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}

/// <summary>Cache-aside reads, write-then-invalidate writes.</summary>
public sealed class ProductService(
    IProductRepository repository, ProductCache cache, IOptions<CacheOptions> options) : IProductService
{
    private TimeSpan ProductTtl => TimeSpan.FromSeconds(options.Value.ProductSeconds);
    private TimeSpan CatalogTtl => TimeSpan.FromSeconds(options.Value.CatalogSeconds);

    public Task<CacheResult<Product?>> GetProductAsync(int id, CancellationToken ct) =>
        cache.GetOrCreateAsync(
            ProductCache.ProductKey(id), () => repository.GetByIdAsync(id, ct), ProductTtl, isCatalog: false, ct);

    public Task<CacheResult<Catalog?>> GetCatalogAsync(string? category, CancellationToken ct)
    {
        category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

        return cache.GetOrCreateAsync(
            ProductCache.CatalogKey(category),
            async () => (Catalog?)await repository.GetCatalogAsync(category, ct),
            CatalogTtl, isCatalog: true, ct);
    }

    public async Task<Product> CreateAsync(ProductInput input, CancellationToken ct)
    {
        var id = await repository.CreateAsync(input, ct);
        cache.InvalidateCatalog();
        return new Product(id, input.Name, input.Category, input.Price, input.Stock);
    }

    public async Task<bool> UpdateAsync(int id, ProductInput input, CancellationToken ct)
    {
        var updated = await repository.UpdateAsync(id, input, ct);
        if (updated)
        {
            cache.InvalidateProduct(id);
            cache.InvalidateCatalog();
        }

        return updated;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var deleted = await repository.DeleteAsync(id, ct);
        if (deleted)
        {
            cache.InvalidateProduct(id);
            cache.InvalidateCatalog();
        }

        return deleted;
    }
}
