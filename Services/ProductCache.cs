using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DataService.Services;

public sealed record CacheResult<T>(T Value, bool FromCache);

/// <summary>
/// Thin wrapper over <see cref="IMemoryCache"/> that adds three things the raw API lacks:
/// stampede protection (one loader per key), bounded size, and group invalidation of catalog entries.
/// </summary>
public sealed class ProductCache(IMemoryCache cache)
{
    // Striped locks: bounded memory, and concurrent misses for the same key share one database call.
    private readonly SemaphoreSlim[] _stripes = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private readonly object _tokenLock = new();
    private CancellationTokenSource _catalogTokenSource = new();

    public static string ProductKey(int id) => $"product:{id}";

    // SQL Server's default collation is case-insensitive, so the key is too.
    public static string CatalogKey(string? category) => $"catalog:{category?.ToLowerInvariant() ?? "*"}";

    /// <summary>
    /// Returns the cached value or loads it. Null results are never cached, so unknown ids
    /// cannot be used to fill the cache.
    /// </summary>
    public async Task<CacheResult<T?>> GetOrCreateAsync<T>(
        string key, Func<Task<T?>> load, TimeSpan ttl, bool isCatalog, CancellationToken ct) where T : class
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return new(cached, true);
        }

        var gate = _stripes[(uint)key.GetHashCode() % (uint)_stripes.Length];
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(key, out cached) && cached is not null)
            {
                return new(cached, true);
            }

            // Captured BEFORE loading: if a write invalidates the catalog while we load,
            // the token is already cancelled and the (possibly stale) entry expires immediately.
            var catalogToken = CurrentCatalogToken();

            var value = await load();
            if (value is null)
            {
                return new(null, false);
            }

            var entry = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Size = 1 };
            if (isCatalog)
            {
                entry.AddExpirationToken(new CancellationChangeToken(catalogToken));
            }

            cache.Set(key, value, entry);
            return new(value, false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void InvalidateProduct(int id) => cache.Remove(ProductKey(id));

    /// <summary>Expires every cached catalog at once (any write can change any list).</summary>
    public void InvalidateCatalog()
    {
        CancellationTokenSource previous;
        lock (_tokenLock)
        {
            previous = _catalogTokenSource;
            _catalogTokenSource = new CancellationTokenSource();
        }

        previous.Cancel(); // no timers or handles, so there is nothing to Dispose
    }

    private CancellationToken CurrentCatalogToken()
    {
        lock (_tokenLock)
        {
            return _catalogTokenSource.Token;
        }
    }
}
