using System.Data;
using DataService.Models;
using Microsoft.Data.SqlClient;

namespace DataService.Data;

public sealed class OrderRepository(ISqlConnectionFactory connections) : IOrderRepository
{
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            var createdUtc = DateTime.UtcNow;
            var orderId = await InsertOrderAsync(connection, transaction, request.CustomerName, createdUtc, ct);

            var lines = new List<OrderLine>();
            var total = 0m;

            // Locking products in a consistent order prevents deadlocks between concurrent orders.
            foreach (var item in request.Items.OrderBy(i => i.ProductId))
            {
                var unitPrice = await ReserveStockAsync(connection, transaction, item, ct);
                await InsertLineAsync(connection, transaction, orderId, item, unitPrice, ct);

                lines.Add(new OrderLine(item.ProductId, item.Quantity, unitPrice));
                total += unitPrice * item.Quantity;
            }

            await UpdateTotalAsync(connection, transaction, orderId, total, ct);
            await transaction.CommitAsync(ct);

            return new Order(orderId, request.CustomerName, createdUtc, total, lines);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None); // roll back even if the request was cancelled
            throw;
        }
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, CustomerName, CreatedUtc, Total FROM dbo.Orders WHERE Id = @Id;
            SELECT ProductId, Quantity, UnitPrice FROM dbo.OrderItems WHERE OrderId = @Id ORDER BY Id;
            """;

        await using var connection = connections.Create();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var customerName = reader.GetString(1);
        var createdUtc = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc);
        var total = reader.GetDecimal(3);

        await reader.NextResultAsync(ct);
        var lines = new List<OrderLine>();
        while (await reader.ReadAsync(ct))
        {
            lines.Add(new OrderLine(reader.GetInt32(0), reader.GetInt32(1), reader.GetDecimal(2)));
        }

        return new Order(id, customerName, createdUtc, total, lines);
    }

    // ---- Steps that run inside the transaction -------------------------------------------

    private static async Task<int> InsertOrderAsync(
        SqlConnection connection, SqlTransaction transaction, string customerName, DateTime createdUtc, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO dbo.Orders (CustomerName, CreatedUtc, Total)
            OUTPUT INSERTED.Id
            VALUES (@CustomerName, @CreatedUtc, 0)
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CustomerName", SqlDbType.NVarChar, 100).Value = customerName;
        command.Parameters.Add("@CreatedUtc", SqlDbType.DateTime2).Value = createdUtc;
        return (int)(await command.ExecuteScalarAsync(ct))!;
    }

    /// <summary>
    /// Decrements stock only when enough is available and returns the current price in the same
    /// statement, so the check and the update cannot be separated by another request.
    /// </summary>
    private static async Task<decimal> ReserveStockAsync(
        SqlConnection connection, SqlTransaction transaction, OrderLineRequest item, CancellationToken ct)
    {
        const string sql = """
            UPDATE dbo.Products
            SET Stock = Stock - @Quantity
            OUTPUT INSERTED.Price
            WHERE Id = @ProductId AND Stock >= @Quantity
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@ProductId", SqlDbType.Int).Value = item.ProductId;
        command.Parameters.Add("@Quantity", SqlDbType.Int).Value = item.Quantity;

        var price = await command.ExecuteScalarAsync(ct);
        return price is decimal unitPrice
            ? unitPrice
            : throw new InsufficientStockException(item.ProductId);
    }

    private static async Task InsertLineAsync(
        SqlConnection connection, SqlTransaction transaction, int orderId, OrderLineRequest item, decimal unitPrice, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO dbo.OrderItems (OrderId, ProductId, Quantity, UnitPrice)
            VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
        command.Parameters.Add("@ProductId", SqlDbType.Int).Value = item.ProductId;
        command.Parameters.Add("@Quantity", SqlDbType.Int).Value = item.Quantity;
        command.Parameters.AddDecimal("@UnitPrice", unitPrice, 10, 2);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpdateTotalAsync(
        SqlConnection connection, SqlTransaction transaction, int orderId, decimal total, CancellationToken ct)
    {
        const string sql = "UPDATE dbo.Orders SET Total = @Total WHERE Id = @OrderId";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@OrderId", SqlDbType.Int).Value = orderId;
        command.Parameters.AddDecimal("@Total", total, 12, 2);
        await command.ExecuteNonQueryAsync(ct);
    }
}
