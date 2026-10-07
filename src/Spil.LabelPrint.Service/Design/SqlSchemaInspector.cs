using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Spil.LabelPrint.Service.Design;

public sealed class DbInspectRequest
{
    public string? Server { get; set; }
    public int Port { get; set; } = 1433;
    public string? Database { get; set; }
    public string? AuthType { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? Schema { get; set; }
    public string? Table { get; set; }
}

public sealed class DbTableInfo
{
    public string Schema { get; set; } = "dbo";
    public string Name { get; set; } = "";
}

public sealed class SqlSchemaInspector
{
    public const int MaxFields = 600;
    public const int MaxTables = 2000;

    public async Task<object> InspectAsync(DbInspectRequest req, CancellationToken ct)
    {
        var server = (req.Server ?? "").Trim();
        var database = (req.Database ?? "").Trim();
        if (string.IsNullOrWhiteSpace(server))
            throw new ArgumentException("Server is required.");
        if (string.IsNullOrWhiteSpace(database))
            throw new ArgumentException("Database is required.");
        if (!SafeName(database))
            throw new ArgumentException("Database name contains invalid characters.");

        var schemaFilter = string.IsNullOrWhiteSpace(req.Schema) ? null : req.Schema.Trim();
        var tableFilter = string.IsNullOrWhiteSpace(req.Table) ? null : req.Table.Trim();
        if (tableFilter != null && tableFilter.Contains('.', StringComparison.Ordinal) && schemaFilter == null)
        {
            var dot = tableFilter.LastIndexOf('.');
            schemaFilter = tableFilter[..dot];
            tableFilter = tableFilter[(dot + 1)..];
        }
        if (schemaFilter != null && !SafeName(schemaFilter))
            throw new ArgumentException("Schema name contains invalid characters.");
        if (tableFilter != null && !SafeName(tableFilter))
            throw new ArgumentException("Table name contains invalid characters.");

        var cs = BuildConnectionString(server, req.Port <= 0 ? 1433 : req.Port, database, req);
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);

        var tables = await LoadTablesAsync(conn, schemaFilter, ct);
        if (tableFilter != null)
        {
            tables = tables
                .Where(t => t.Name.Equals(tableFilter, StringComparison.OrdinalIgnoreCase)
                    && (schemaFilter == null || t.Schema.Equals(schemaFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (tables.Count == 0)
                throw new InvalidOperationException($"Table '{tableFilter}' was not found in {database}.");
        }

        var fields = await LoadColumnsAsync(conn, schemaFilter, tableFilter, tables.Count == 1, ct);
        return new
        {
            ok = true,
            database,
            schema = schemaFilter,
            table = tableFilter,
            truncated = fields.Count >= MaxFields || tables.Count >= MaxTables,
            tables,
            fields,
        };
    }

    private static async Task<List<DbTableInfo>> LoadTablesAsync(SqlConnection conn, string? schema, CancellationToken ct)
    {
        const string sql = """
            SELECT TOP (@max) TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
              AND (@schema IS NULL OR TABLE_SCHEMA = @schema)
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 8 };
        cmd.Parameters.AddWithValue("@max", MaxTables);
        cmd.Parameters.AddWithValue("@schema", (object?)schema ?? DBNull.Value);
        var list = new List<DbTableInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DbTableInfo
            {
                Schema = reader.GetString(0),
                Name = reader.GetString(1),
            });
        }
        return list;
    }

    private static async Task<List<FieldCatalogItem>> LoadColumnsAsync(
        SqlConnection conn, string? schema, string? table, bool singleTable, CancellationToken ct)
    {
        const string sql = """
            SELECT TOP (@max) TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE (@schema IS NULL OR TABLE_SCHEMA = @schema)
              AND (@table IS NULL OR TABLE_NAME = @table)
            ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION
            """;
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 8 };
        cmd.Parameters.AddWithValue("@max", MaxFields);
        cmd.Parameters.AddWithValue("@schema", (object?)schema ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@table", (object?)table ?? DBNull.Value);
        var list = new List<FieldCatalogItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sch = reader.GetString(0);
            var tbl = reader.GetString(1);
            var col = reader.GetString(2);
            var dataType = reader.GetString(3);
            var key = singleTable || table != null ? col : $"{tbl}.{col}";
            list.Add(new FieldCatalogItem
            {
                Key = key,
                Label = Humanize(col),
                Type = MapType(dataType),
                Source = $"{sch}.{tbl}",
            });
        }
        return list;
    }

    private static string BuildConnectionString(string server, int port, string database, DbInspectRequest req)
    {
        var namedInstance = server.Contains('\\', StringComparison.Ordinal);
        var dataSource = namedInstance && port == 1433 ? server : $"{server},{port}";
        var b = new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = database,
            TrustServerCertificate = true,
            ConnectTimeout = 8,
            ApplicationName = "SPIL Label Print Service",
        };
        var windows = !string.Equals(req.AuthType, "sql", StringComparison.OrdinalIgnoreCase);
        if (windows)
        {
            b.IntegratedSecurity = true;
        }
        else
        {
            b.IntegratedSecurity = false;
            b.UserID = (req.User ?? "").Trim();
            b.Password = req.Password ?? "";
            if (string.IsNullOrEmpty(b.UserID))
                throw new ArgumentException("SQL user is required when signing in with a SQL login.");
        }
        return b.ConnectionString;
    }

    private static bool SafeName(string name) =>
        name.Length is > 0 and <= 128
        && name.IndexOfAny([';', '\'', '"', '[', ']', '\n', '\r', '\0']) < 0;

    private static string Humanize(string column)
    {
        if (string.IsNullOrEmpty(column)) return column;
        var spaced = Regex.Replace(column, "([a-z])([A-Z])", "$1 $2");
        spaced = Regex.Replace(spaced, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        return spaced.Replace('_', ' ').Trim();
    }

    private static string MapType(string sqlType)
    {
        var t = (sqlType ?? "").ToLowerInvariant();
        if (t.Contains("date") || t.Contains("time")) return "text";
        if (t is "image" or "varbinary" or "binary" or "timestamp") return "text";
        return "text";
    }
}
