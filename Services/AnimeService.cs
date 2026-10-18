using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MediaManager.Data;
using MediaManager.Models;
using Microsoft.Extensions.Configuration;

namespace MediaManager.Services;

public class AnimeService
{
    private readonly HttpClient _httpClient;
    private readonly MediaRepository _repository;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AnimeService(HttpClient httpClient, MediaRepository repository)
    {
        _httpClient = httpClient;
        _repository = repository;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MediaManager/1.0");
        // Kitsu aceita JSON padrão da API deles
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
    }

    public async Task<Anime?> CreateAnimeAsync(
        string title,
        double ratingValue,
        bool manga,
        string animeType,
        string dateWatched,
        string review)
    {
        var animeInfo = await SearchKitsuAsync(title);

        if (animeInfo == null)
            return null;

        var anime = new Anime
        {
            Id = CreateSlug(title),
            Title = title,
            Episodes = animeInfo.Episodes,
            Rating = ratingValue,
            DateWatched = dateWatched,
            AnimeType = animeType,
            Manga = manga,
            Review = review,
            Poster = animeInfo.Poster
        };

        await _repository.InsertAnimeAsync(anime);

        return anime;
    }

    private async Task<KitsuAnimeInfo?> SearchKitsuAsync(string title)
    {
        try
        {
            var encodedTitle = Uri.EscapeDataString(title);
            // URL de busca da Kitsu API v1
            var url = $"https://kitsu.io/api/edge/anime?filter[text]={encodedTitle}&page[limit]=1";

            Console.WriteLine();
            Console.WriteLine("=== KITSU API ===");
            Console.WriteLine($"Searching: {title}");
            Console.WriteLine($"URL: {url}");

            using var response = await _httpClient.GetAsync(url);

            Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Kitsu returned {(int)response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();

            var data = JsonSerializer.Deserialize<KitsuResponse>(json, _jsonOptions);

            if (data?.Data == null || data.Data.Count == 0)
            {
                Console.WriteLine("Kitsu returned zero results.");
                return null;
            }

            var found = data.Data[0];
            var attributes = found.Attributes;

            var poster = attributes?.PosterImage?.Large 
                         ?? attributes?.PosterImage?.Original 
                         ?? attributes?.PosterImage?.Medium 
                         ?? "";

            var episodes = attributes?.EpisodeCount?.ToString() ?? "";

            Console.WriteLine($"Found: {attributes?.CanonicalTitle}");
            Console.WriteLine($"Episodes: {episodes}");
            Console.WriteLine($"Poster: {poster}");

            return new KitsuAnimeInfo
            {
                Episodes = episodes,
                Poster = poster
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Kitsu ERROR: {ex}");
            return null;
        }
    }

    private static string CreateSlug(string title)
    {
        var chars = title
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        var slug = new string(chars);

        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        return slug.Trim('-');
    }

    // Classes de mapeamento para o JSON da Kitsu API
    private class KitsuResponse
    {
        [JsonPropertyName("data")]
        public List<KitsuItem> Data { get; set; } = new();
    }

    private class KitsuItem
    {
        [JsonPropertyName("attributes")]
        public KitsuAttributes? Attributes { get; set; }
    }

    private class KitsuAttributes
    {
        [JsonPropertyName("canonicalTitle")]
        public string? CanonicalTitle { get; set; }

        [JsonPropertyName("episodeCount")]
        public int? EpisodeCount { get; set; }

        [JsonPropertyName("posterImage")]
        public KitsuPosterImage? PosterImage { get; set; }
    }

    private class KitsuPosterImage
    {
        [JsonPropertyName("tiny")]
        public string? Tiny { get; set; }

        [JsonPropertyName("small")]
        public string? Small { get; set; }

        [JsonPropertyName("medium")]
        public string? Medium { get; set; }

        [JsonPropertyName("large")]
        public string? Large { get; set; }

        [JsonPropertyName("original")]
        public string? Original { get; set; }
    }

    private class KitsuAnimeInfo
    {
        public string Episodes { get; set; } = "";
        public string Poster { get; set; } = "";
    }
}
