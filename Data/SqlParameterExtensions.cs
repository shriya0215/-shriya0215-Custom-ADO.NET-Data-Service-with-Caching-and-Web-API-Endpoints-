using System.Data;
using Microsoft.Data.SqlClient;

namespace DataService.Data;

internal static class SqlParameterExtensions
{
    /// <summary>Adds a DECIMAL parameter with explicit precision/scale so SQL Server never guesses.</summary>
    public static SqlParameter AddDecimal(
        this SqlParameterCollection parameters, string name, decimal value, byte precision, byte scale)
    {
        var parameter = parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = precision;
        parameter.Scale = scale;
        parameter.Value = value;
        return parameter;
    }
}
