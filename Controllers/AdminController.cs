using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MediaManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace MediaManager.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(
    IConfiguration configuration,
    ExternalApiKeyStore apiKeys) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var configuredUsername = configuration["Admin:Username"] ?? "";
        var configuredPassword = configuration["Admin:Password"] ?? "";
        if (string.IsNullOrWhiteSpace(configuredUsername) ||
            string.IsNullOrWhiteSpace(configuredPassword) ||
            !FixedEquals(configuredUsername, request.Username) ||
            !FixedEquals(configuredPassword, request.Password))
            return Unauthorized(new { message = "Incorrect username or password, or the admin account is not configured." });

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, configuredUsername)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
        return Ok(new { authenticated = true });
    }

    [Authorize]
    [HttpGet("session")]
    public IActionResult Session() => Ok(new { authenticated = true, username = User.Identity?.Name });

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [Authorize]
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            return Ok(new
            {
                googleBooksApiKeyConfigured = await apiKeys.IsConfiguredAsync("GoogleBooksApiKey"),
                tmdbApiKeyConfigured = await apiKeys.IsConfiguredAsync("TmdbApiKey"),
                rawgApiKeyConfigured = await apiKeys.IsConfiguredAsync("RawgApiKey")
            });
        }
        catch (InvalidOperationException error)
        {
            return Problem(error.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [Authorize]
    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(ApiKeysRequest request)
    {
        try
        {
            await apiKeys.SaveAsync("GoogleBooksApiKey", request.GoogleBooksApiKey ?? "");
            await apiKeys.SaveAsync("TmdbApiKey", request.TmdbApiKey ?? "");
            await apiKeys.SaveAsync("RawgApiKey", request.RawgApiKey ?? "");
        }
        catch (InvalidOperationException error)
        {
            return Problem(error.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return await GetSettings();
    }

    private static bool FixedEquals(string expected, string? actual)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(actual ?? ""));
        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}

public sealed record LoginRequest(string Username, string Password);
public sealed record ApiKeysRequest(string? GoogleBooksApiKey, string? TmdbApiKey, string? RawgApiKey);
