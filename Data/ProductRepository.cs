using System.Data;
using DataService.Models;
using Microsoft.Data.SqlClient;

namespace DataService.Data;

public sealed class ProductRepository(ISqlConnectionFactory connections) : IProductRepository
{
    private const int SqlForeignKeyViolation = 547;

    // ---------------------------------------------------------------------
    // Connected model: open -> execute -> close, every value is a parameter.
    // ---------------------------------------------------------------------

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT Id, Name, Category, Price, Stock FROM dbo.Products WHERE Id = @Id";

        await using var connection = connections.Create();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<int> CreateAsync(ProductInput input, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Products (Name, Category, Price, Stock)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Category, @Price, @Stock)
            """;

        await using var connection = connections.Create();
        await using var command = new SqlCommand(sql, connection);
        AddProductParameters(command, input);

        await connection.OpenAsync(ct);
        return (int)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task<bool> UpdateAsync(int id, ProductInput input, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Products
            SET Name = @Name, Category = @Category, Price = @Price, Stock = @Stock
            WHERE Id = @Id
            """;

        await using var connection = connections.Create();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        AddProductParameters(command, input);

        await connection.OpenAsync(ct);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM dbo.Products WHERE Id = @Id";

        await using var connection = connections.Create();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        try
        {
            await connection.OpenAsync(ct);
            return await command.ExecuteNonQueryAsync(ct) > 0;
        }
        catch (SqlException ex) when (ex.Number == SqlForeignKeyViolation)
        {
            throw new ProductInUseException(id);
        }
    }

    // ---------------------------------------------------------------------
    // Disconnected model: SqlDataAdapter fills a DataSet and releases the
    // connection immediately. One round trip returns two result sets.
    // ---------------------------------------------------------------------

    // SqlDataAdapter.Fill has no async API, so the blocking call is moved off the request thread.
    public Task<Catalog> GetCatalogAsync(string? category, CancellationToken ct = default) =>
        Task.Run(() => LoadCatalog(category), ct);

    private Catalog LoadCatalog(string? category)
    {
        const string sql = """
            SELECT Id, Name, Category, Price, Stock
            FROM dbo.Products
            WHERE @Category IS NULL OR Category = @Category
            ORDER BY Name;

            SELECT Category, COUNT(*) AS ProductCount, AVG(Price) AS AveragePrice
            FROM dbo.Products
            WHERE @Category IS NULL OR Category = @Category
            GROUP BY Category
            ORDER BY Category;
            """;

        using var connection = connections.Create();
        using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Category", SqlDbType.NVarChar, 50).Value = (object?)category ?? DBNull.Value;

        using var adapter = new SqlDataAdapter(command);
        adapter.TableMappings.Add("Table", "Products");
        adapter.TableMappings.Add("Table1", "CategorySummary");

        var dataSet = new DataSet();
        adapter.Fill(dataSet); // opens and closes the connection itself

        var products = dataSet.Tables["Products"]!.AsEnumerable()
            .Select(r => new Product(
                r.Field<int>("Id"),
                r.Field<string>("Name")!,
                r.Field<string>("Category")!,
                r.Field<decimal>("Price"),
                r.Field<int>("Stock")))
            .ToList();

        var summary = dataSet.Tables["CategorySummary"]!.AsEnumerable()
            .Select(r => new CategorySummary(
                r.Field<string>("Category")!,
                r.Field<int>("ProductCount"),
                decimal.Round(r.Field<decimal>("AveragePrice"), 2)))
            .ToList();

        return new Catalog(products, summary);
    }

    // ---------------------------------------------------------------------

    private static Product Map(IDataRecord r) =>
        new(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.GetInt32(4));

    private static void AddProductParameters(SqlCommand command, ProductInput input)
    {
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = input.Name;
        command.Parameters.Add("@Category", SqlDbType.NVarChar, 50).Value = input.Category;
        command.Parameters.AddDecimal("@Price", input.Price, 10, 2);
        command.Parameters.Add("@Stock", SqlDbType.Int).Value = input.Stock;
    }
}
