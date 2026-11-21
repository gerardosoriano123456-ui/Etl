using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace OpinionesDW;

internal sealed class ReviewSource(IConfiguration config) : ISource
{
    public string Code => "resena";
    public async IAsyncEnumerable<SourceRecord> ReadAsync([EnumeratorCancellation] CancellationToken token)
    {
        string provider = config["Sources:ReviewProvider"] ?? "SQLite";
        string value = config.GetConnectionString("Reviews")
            ?? throw new InvalidOperationException("Configura ConnectionStrings:Reviews.");
        await using DbConnection db = provider switch
        {
            "SQLServer" => ConnectSqlServer(value),
            "SQLite" => new SqliteConnection(value),
            _ => throw new InvalidOperationException("Proveedor de reseñas desconocido.")
        };
        string qualifier = provider == "SQLServer" ? "dbo." : "";
        await db.OpenAsync(token);
        await using var command = db.CreateCommand();
        command.CommandText = $"""
            SELECT r.id_opinion, o.id_producto, o.fecha_opinion, o.texto, o.calificacion, r.sitio_web
            FROM {qualifier}resena_web AS r
            JOIN {qualifier}opinion AS o ON o.id_opinion = r.id_opinion
            ORDER BY r.id_opinion
            """;
        command.CommandTimeout = 30;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token);
        while (await reader.ReadAsync(token))
        {
            string id = reader.GetInt64(0).ToString(CultureInfo.InvariantCulture);
            yield return new SourceRecord(id, new
            {
                IdProducto = reader.GetInt32(1),
                FechaOpinion = provider == "SQLServer"
                    ? reader.GetDateTime(2).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    : reader.GetString(2),
                Texto = reader.GetString(3),
                Calificacion = reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                SitioWeb = reader.GetString(5)
            });
        }
    }

    private static SqlConnection ConnectSqlServer(string value)
    {
        var settings = new SqlConnectionStringBuilder(value);
        if (settings.TrustServerCertificate || settings.Encrypt == SqlConnectionEncryptOption.Optional)
            throw new InvalidOperationException("SQL Server requiere cifrado y validación del certificado.");
        return new SqlConnection(settings.ConnectionString);
    }
}
