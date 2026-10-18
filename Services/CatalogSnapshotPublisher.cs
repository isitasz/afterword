namespace MediaManager.Services;

/// <summary>
/// Requests a new static Vercel deployment after a catalog write.
/// The deploy hook URL is a secret and must only come from server configuration.
/// </summary>
public sealed class CatalogSnapshotPublisher(
    HttpClient httpClient,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<CatalogSnapshotPublisher> logger)
{
    public async Task PublishAsync()
    {
        var hookUrl = configuration["VERCEL_DEPLOY_HOOK"]?.Trim();
        if (string.IsNullOrWhiteSpace(hookUrl))
        {
            if (environment.IsDevelopment())
                logger.LogDebug("No Vercel deployment was requested because this is a local development run.");
            else
                logger.LogWarning("Catalog snapshot was not published because VERCEL_DEPLOY_HOOK is not configured.");
            return;
        }

        if (!Uri.TryCreate(hookUrl, UriKind.Absolute, out var hookUri) || hookUri.Scheme != Uri.UriSchemeHttps)
        {
            logger.LogError("Catalog snapshot was not published because VERCEL_DEPLOY_HOOK is not a valid HTTPS URL.");
            return;
        }

        try
        {
            using var response = await httpClient.PostAsync(hookUri, content: null);
            if (!response.IsSuccessStatusCode)
                logger.LogError("Vercel rejected the catalog snapshot deployment request with status {StatusCode}.",
                    (int)response.StatusCode);
        }
        catch (Exception error)
        {
            // Do not log the exception message: it could contain the secret deploy-hook URL.
            logger.LogError("Could not trigger a catalog snapshot deployment ({ErrorType}).", error.GetType().Name);
        }
    }
}
