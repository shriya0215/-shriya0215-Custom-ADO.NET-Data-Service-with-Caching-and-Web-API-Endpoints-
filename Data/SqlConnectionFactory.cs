using Microsoft.Data.SqlClient;

namespace DataService.Data;

public interface ISqlConnectionFactory
{
    /// <summary>Creates a new, unopened connection. The caller owns and disposes it.</summary>
    SqlConnection Create();
}

/// <summary>
/// Hands out short-lived connections. ADO.NET pooling makes open/close per operation cheap,
/// so repositories never keep a connection alive between calls.
/// </summary>
public sealed class SqlConnectionFactory(IConfiguration configuration) : ISqlConnectionFactory
{
    private readonly string _connectionString =
        configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

    public SqlConnection Create() => new(_connectionString);
}
