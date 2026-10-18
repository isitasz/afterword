using MediaManager.Data;
using MediaManager.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Permite reutilizar a configuração local do antigo app desktop.
// Variáveis de ambiente são adicionadas depois e têm prioridade sobre o arquivo.
builder.Configuration
    .AddJsonFile("configuration.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "afterword.admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<BookRepository>();
builder.Services.AddScoped<MediaRepository>();
builder.Services.AddScoped<ApiSettingsRepository>();
builder.Services.AddScoped<ExternalApiKeyStore>();
builder.Services.AddHttpClient<BookService>();
builder.Services.AddHttpClient<GameService>();
builder.Services.AddHttpClient<MovieService>();
builder.Services.AddHttpClient<AnimeService>();
builder.Services.AddHttpClient<CatalogSnapshotPublisher>();

var app = builder.Build();

// During local development, serve the static site from the API project for convenience.
// In production, Vercel serves wwwroot and forwards /api requests to this service.
if (app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

// Garante que a estrutura mínima do banco exista antes de atender requisições.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<BookRepository>().InitializeAsync();
    await scope.ServiceProvider.GetRequiredService<MediaRepository>().InitializeAsync();
    await scope.ServiceProvider.GetRequiredService<ApiSettingsRepository>().InitializeAsync();
}

app.MapGet("/api/health", () => Results.Ok(new
{
    name = "Afterword API",
    status = "online"
}));

if (app.Environment.IsDevelopment())
{
    app.MapGet("/admin", async context =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath!, "admin.html"));
    });
}

app.MapControllers();

app.Run();
