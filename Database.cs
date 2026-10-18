using System;
using Npgsql;

namespace MediaManager.Data;

public static class Database
{
    public static NpgsqlConnection GetConnection()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("NEON_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A variável de ambiente NEON_CONNECTION_STRING não está configurada.");
        }

        return new NpgsqlConnection(ToNpgsqlConnectionString(connectionString));
    }

    private static string ToNpgsqlConnectionString(string connectionString)
    {
        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) ||
            (!uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase) &&
             !uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase)))
        {
            // Npgsql's regular Host=...;Database=... format can be used as-is.
            return connectionString;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        if (userInfo.Length != 2 ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException(
                "A URL do Neon em NEON_CONNECTION_STRING está incompleta.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port < 1 ? 5432 : uri.Port,
            Database = database,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.Require
        };

        foreach (var item in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).ToLowerInvariant();
            var value = parts.Length == 2
                ? Uri.UnescapeDataString(parts[1])
                : "";

            var npgsqlKey = key switch
            {
                "sslmode" => "SSL Mode",
                "channel_binding" => "Channel Binding",
                "application_name" => "Application Name",
                "connect_timeout" => "Timeout",
                "target_session_attrs" => "Target Session Attributes",
                "options" => "Options",
                _ => null
            };

            if (npgsqlKey != null)
                builder[npgsqlKey] = value;
        }

        return builder.ConnectionString;
    }
}
