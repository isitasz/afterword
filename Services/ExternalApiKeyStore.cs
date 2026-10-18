using System.Security.Cryptography;
using System.Text;
using MediaManager.Data;

namespace MediaManager.Services;

public sealed class ExternalApiKeyStore(ApiSettingsRepository repository, IConfiguration configuration)
{
    private static readonly string[] KeyNames =
    [
        "GoogleBooksApiKey",
        "TmdbApiKey",
        "RawgApiKey"
    ];

    public async Task<string> GetAsync(string keyName)
    {
        var encrypted = await repository.GetAsync(keyName);
        if (encrypted is not null)
        {
            var key = GetEncryptionKey();
            var plaintext = new byte[encrypted.Value.Value.Length];
            using var aes = new AesGcm(key, encrypted.Value.Tag.Length);
            aes.Decrypt(encrypted.Value.Nonce, encrypted.Value.Value,
                encrypted.Value.Tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }

        return configuration[$"ExternalApis:{keyName}"]
            ?? configuration[keyName]
            ?? "";
    }

    public async Task<bool> IsConfiguredAsync(string keyName) =>
        !string.IsNullOrWhiteSpace(await GetAsync(keyName));

    public async Task SaveAsync(string keyName, string value)
    {
        if (!KeyNames.Contains(keyName, StringComparer.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(keyName));
        if (string.IsNullOrWhiteSpace(value)) return;

        var key = GetEncryptionKey();
        var plaintext = Encoding.UTF8.GetBytes(value.Trim());
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        await repository.SaveAsync(keyName, ciphertext, nonce, tag);
        CryptographicOperations.ZeroMemory(plaintext);
    }

    private byte[] GetEncryptionKey()
    {
        var configured = Environment.GetEnvironmentVariable("APP_SETTINGS_ENCRYPTION_KEY")
            ?? configuration["AppSettingsEncryptionKey"];
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "Configure APP_SETTINGS_ENCRYPTION_KEY no ambiente do servidor antes de salvar chaves.");

        try
        {
            var key = Convert.FromBase64String(configured);
            if (key.Length != 32) throw new FormatException();
            return key;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "APP_SETTINGS_ENCRYPTION_KEY precisa ser uma chave Base64 de 32 bytes.");
        }
    }
}
