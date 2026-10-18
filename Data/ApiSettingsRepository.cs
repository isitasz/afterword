using Npgsql;

namespace MediaManager.Data;

public sealed class ApiSettingsRepository
{
    public async Task InitializeAsync()
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        const string sql = """
            CREATE TABLE IF NOT EXISTS app_secrets (
                name TEXT PRIMARY KEY,
                encrypted_value BYTEA NOT NULL,
                nonce BYTEA NOT NULL,
                authentication_tag BYTEA NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<(byte[] Value, byte[] Nonce, byte[] Tag)?> GetAsync(string name)
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        const string sql = "SELECT encrypted_value, nonce, authentication_tag FROM app_secrets WHERE name=@name";
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("name", name);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return ((byte[])reader[0], (byte[])reader[1], (byte[])reader[2]);
    }

    public async Task SaveAsync(string name, byte[] value, byte[] nonce, byte[] tag)
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        const string sql = """
            INSERT INTO app_secrets (name, encrypted_value, nonce, authentication_tag)
            VALUES (@name, @value, @nonce, @tag)
            ON CONFLICT (name) DO UPDATE SET encrypted_value=EXCLUDED.encrypted_value,
                nonce=EXCLUDED.nonce, authentication_tag=EXCLUDED.authentication_tag,
                updated_at=NOW();
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("value", value);
        command.Parameters.AddWithValue("nonce", nonce);
        command.Parameters.AddWithValue("tag", tag);
        await command.ExecuteNonQueryAsync();
    }
}
