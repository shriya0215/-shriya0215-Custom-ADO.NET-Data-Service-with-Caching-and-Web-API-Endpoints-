using DataService.Models;

namespace DataService.Data;

public interface IProductRepository
{
    // Connected model (SqlConnection / SqlCommand / SqlDataReader)
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(ProductInput input, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, ProductInput input, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    // Disconnected model (SqlDataAdapter / DataSet)
    Task<Catalog> GetCatalogAsync(string? category, CancellationToken ct = default);
}
