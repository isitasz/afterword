using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MediaManager.Data;
using MediaManager.Models;
using System.Text.Json.Serialization;

namespace MediaManager.Services;

public class GameService
{
    private readonly ExternalApiKeyStore _apiKeys;

    private readonly HttpClient _httpClient;
    private readonly MediaRepository _repository;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public GameService(HttpClient httpClient, MediaRepository repository, ExternalApiKeyStore apiKeys)
    {
        _apiKeys = apiKeys;
        _httpClient = httpClient;
        _repository = repository;

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "MediaManager/1.0");
    }

    public async Task<Game?> CreateGameAsync(
        string title,
        string hours,
        double rating,
        bool owned,
        string datePlayed,
        string review)
    {
        var rawgGame = await SearchRawgAsync(title);

        if (rawgGame == null)
            return null;

        var game = new Game
        {
            Id = CreateSlug(title),
            Title = title,
            Link = rawgGame.Link,
            Rating = rating,
            Hours = hours,
            DatePlayed = datePlayed,
            Owned = owned,
            Review = review,
            Poster = rawgGame.Poster
        };

        await _repository.InsertGameAsync(game);

        return game;
    }

    private async Task<Game?> SearchRawgAsync(string title)
    {
        try
        {
            var rawgApiKey = await _apiKeys.GetAsync("RawgApiKey");
            var encodedTitle = Uri.EscapeDataString(title);

            var url =
                $"https://api.rawg.io/api/games" +
                $"?search={encodedTitle}" +
                $"&key={Uri.EscapeDataString(rawgApiKey)}";

            Console.WriteLine();
            Console.WriteLine($"=== RAWG: {title} ===");

            using var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"RAWG returned {(int)response.StatusCode}");

                return null;
            }

            var json = await response.Content.ReadAsStringAsync();

            var data = JsonSerializer.Deserialize<RawgResponse>(
                json,
                _jsonOptions);

            if (data?.Results == null || data.Results.Count == 0)
            {
                Console.WriteLine("Game not found in RAWG.");
                return null;
            }

            var found = data.Results[0];

            var link = !string.IsNullOrWhiteSpace(found.Slug)
                ? $"https://rawg.io/games/{found.Slug}"
                : "#";

            Console.WriteLine("RAWG game found.");

            return new Game
            {
                Poster = found.BackgroundImage ?? "",
                Link = link
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"RAWG error: {ex.Message}");

            return null;
        }
    }

    private static string CreateSlug(string title)
    {
        var chars = title
            .ToLowerInvariant()
            .Select(c =>
                char.IsLetterOrDigit(c)
                    ? c
                    : '-')
            .ToArray();

        var slug = new string(chars);

        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        return slug.Trim('-');
    }

    private class RawgResponse
    {
        public List<RawgGame> Results { get; set; } = new();
    }

    private class RawgGame
{
    public string? Slug { get; set; }

    [JsonPropertyName("background_image")]
    public string? BackgroundImage { get; set; }
}
}
