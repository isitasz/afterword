using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaManager.Models;
using Npgsql;

namespace MediaManager.Data;

public sealed class MediaRepository
{
    private const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS games (
            database_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            id TEXT NOT NULL,
            title TEXT NOT NULL,
            link TEXT NOT NULL DEFAULT '',
            hours TEXT NOT NULL DEFAULT '',
            rating DOUBLE PRECISION NOT NULL,
            date_played TEXT NOT NULL DEFAULT '',
            owned BOOLEAN NOT NULL,
            review TEXT NOT NULL DEFAULT '',
            poster TEXT NOT NULL DEFAULT '',
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS movies (
            database_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            id TEXT NOT NULL,
            title TEXT NOT NULL,
            year TEXT NOT NULL DEFAULT '',
            rating DOUBLE PRECISION NOT NULL,
            date_watched TEXT NOT NULL DEFAULT '',
            cinema BOOLEAN NOT NULL,
            review TEXT NOT NULL DEFAULT '',
            poster TEXT NOT NULL DEFAULT '',
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS animes (
            database_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            id TEXT NOT NULL,
            title TEXT NOT NULL,
            episodes TEXT NOT NULL DEFAULT '',
            rating DOUBLE PRECISION NOT NULL,
            date_watched TEXT NOT NULL DEFAULT '',
            anime_type TEXT NOT NULL DEFAULT '',
            manga BOOLEAN NOT NULL,
            review TEXT NOT NULL DEFAULT '',
            poster TEXT NOT NULL DEFAULT '',
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        """;

    public async Task InitializeAsync()
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(CreateTablesSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public Task<List<Game>> GetGamesAsync() => QueryManyAsync(
        "SELECT id, title, link, hours, rating, date_played, owned, review, poster FROM games ORDER BY database_id",
        ReadGame);

    public Task<Game?> GetGameAsync(string id) => QueryOneAsync(
        "SELECT id, title, link, hours, rating, date_played, owned, review, poster FROM games WHERE id = @id ORDER BY database_id LIMIT 1",
        id,
        ReadGame);

    public Task<List<Movie>> GetMoviesAsync() => QueryManyAsync(
        "SELECT id, title, year, rating, date_watched, cinema, review, poster FROM movies ORDER BY database_id",
        ReadMovie);

    public Task<Movie?> GetMovieAsync(string id) => QueryOneAsync(
        "SELECT id, title, year, rating, date_watched, cinema, review, poster FROM movies WHERE id = @id ORDER BY database_id LIMIT 1",
        id,
        ReadMovie);

    public Task<List<Anime>> GetAnimesAsync() => QueryManyAsync(
        "SELECT id, title, episodes, rating, date_watched, anime_type, manga, review, poster FROM animes ORDER BY database_id",
        ReadAnime);

    public Task<Anime?> GetAnimeAsync(string id) => QueryOneAsync(
        "SELECT id, title, episodes, rating, date_watched, anime_type, manga, review, poster FROM animes WHERE id = @id ORDER BY database_id LIMIT 1",
        id,
        ReadAnime);

    public Task<bool> UpdateGameAsync(string id, Game game) => ExecuteUpdateAsync(
        "UPDATE games SET title=@title, link=@link, hours=@hours, rating=@rating, date_played=@date_played, owned=@owned, review=@review, poster=@poster WHERE id=@id",
        id,
        command => AddGameParameters(command, game));

    public Task<bool> UpdateMovieAsync(string id, Movie movie) => ExecuteUpdateAsync(
        "UPDATE movies SET title=@title, year=@year, rating=@rating, date_watched=@date_watched, cinema=@cinema, review=@review, poster=@poster WHERE id=@id",
        id,
        command => AddMovieParameters(command, movie));

    public Task<bool> UpdateAnimeAsync(string id, Anime anime) => ExecuteUpdateAsync(
        "UPDATE animes SET title=@title, episodes=@episodes, rating=@rating, date_watched=@date_watched, anime_type=@anime_type, manga=@manga, review=@review, poster=@poster WHERE id=@id",
        id,
        command => AddAnimeParameters(command, anime));

    public Task<bool> DeleteGameAsync(string id) => ExecuteDeleteAsync("DELETE FROM games WHERE id=@id", id);
    public Task<bool> DeleteMovieAsync(string id) => ExecuteDeleteAsync("DELETE FROM movies WHERE id=@id", id);
    public Task<bool> DeleteAnimeAsync(string id) => ExecuteDeleteAsync("DELETE FROM animes WHERE id=@id", id);

    public Task InsertGameAsync(Game game)
    {
        const string insertSql = """
            INSERT INTO games (
                id, title, link, hours, rating, date_played, owned, review, poster
            ) VALUES (
                @id, @title, @link, @hours, @rating, @date_played, @owned, @review, @poster
            );
            """;

        return InsertAsync(insertSql, command => AddGameParameters(command, game));
    }

    public Task InsertMovieAsync(Movie movie)
    {
        const string insertSql = """
            INSERT INTO movies (
                id, title, year, rating, date_watched, cinema, review, poster
            ) VALUES (
                @id, @title, @year, @rating, @date_watched, @cinema, @review, @poster
            );
            """;

        return InsertAsync(insertSql, command => AddMovieParameters(command, movie));
    }

    public Task InsertAnimeAsync(Anime anime)
    {
        const string insertSql = """
            INSERT INTO animes (
                id, title, episodes, rating, date_watched, anime_type, manga, review, poster
            ) VALUES (
                @id, @title, @episodes, @rating, @date_watched, @anime_type, @manga, @review, @poster
            );
            """;

        return InsertAsync(insertSql, command => AddAnimeParameters(command, anime));
    }

    private static async Task InsertAsync(
        string insertSql,
        Action<NpgsqlCommand> addParameters)
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();

        await using (var createTables = new NpgsqlCommand(CreateTablesSql, connection))
        {
            await createTables.ExecuteNonQueryAsync();
        }

        await using var insert = new NpgsqlCommand(insertSql, connection);
        addParameters(insert);
        await insert.ExecuteNonQueryAsync();
    }

    private static void AddGameParameters(NpgsqlCommand command, Game game)
    {
        command.Parameters.AddWithValue("id", game.Id);
        command.Parameters.AddWithValue("title", game.Title);
        command.Parameters.AddWithValue("link", game.Link);
        command.Parameters.AddWithValue("hours", game.Hours);
        command.Parameters.AddWithValue("rating", game.Rating);
        command.Parameters.AddWithValue("date_played", game.DatePlayed);
        command.Parameters.AddWithValue("owned", game.Owned);
        command.Parameters.AddWithValue("review", game.Review);
        command.Parameters.AddWithValue("poster", game.Poster);
    }

    private static void AddMovieParameters(NpgsqlCommand command, Movie movie)
    {
        command.Parameters.AddWithValue("id", movie.Id);
        command.Parameters.AddWithValue("title", movie.Title);
        command.Parameters.AddWithValue("year", movie.Year);
        command.Parameters.AddWithValue("rating", movie.Rating);
        command.Parameters.AddWithValue("date_watched", movie.DateWatched);
        command.Parameters.AddWithValue("cinema", movie.Cinema);
        command.Parameters.AddWithValue("review", movie.Review);
        command.Parameters.AddWithValue("poster", movie.Poster);
    }

    private static void AddAnimeParameters(NpgsqlCommand command, Anime anime)
    {
        command.Parameters.AddWithValue("id", anime.Id);
        command.Parameters.AddWithValue("title", anime.Title);
        command.Parameters.AddWithValue("episodes", anime.Episodes);
        command.Parameters.AddWithValue("rating", anime.Rating);
        command.Parameters.AddWithValue("date_watched", anime.DateWatched);
        command.Parameters.AddWithValue("anime_type", anime.AnimeType);
        command.Parameters.AddWithValue("manga", anime.Manga);
        command.Parameters.AddWithValue("review", anime.Review);
        command.Parameters.AddWithValue("poster", anime.Poster);
    }

    private static Game ReadGame(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetString(0), Title = reader.GetString(1), Link = reader.GetString(2),
        Hours = reader.GetString(3), Rating = reader.GetDouble(4), DatePlayed = reader.GetString(5),
        Owned = reader.GetBoolean(6), Review = reader.GetString(7), Poster = reader.GetString(8)
    };

    private static Movie ReadMovie(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetString(0), Title = reader.GetString(1), Year = reader.GetString(2),
        Rating = reader.GetDouble(3), DateWatched = reader.GetString(4), Cinema = reader.GetBoolean(5),
        Review = reader.GetString(6), Poster = reader.GetString(7)
    };

    private static Anime ReadAnime(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetString(0), Title = reader.GetString(1), Episodes = reader.GetString(2),
        Rating = reader.GetDouble(3), DateWatched = reader.GetString(4), AnimeType = reader.GetString(5),
        Manga = reader.GetBoolean(6), Review = reader.GetString(7), Poster = reader.GetString(8)
    };

    private static async Task<List<T>> QueryManyAsync<T>(string sql, Func<NpgsqlDataReader, T> map)
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<T>();
        while (await reader.ReadAsync()) results.Add(map(reader));
        return results;
    }

    private static async Task<T?> QueryOneAsync<T>(string sql, string id, Func<NpgsqlDataReader, T> map)
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? map(reader) : default;
    }

    private static async Task<bool> ExecuteUpdateAsync(string sql, string id, Action<NpgsqlCommand> addParameters)
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        addParameters(command);
        if (command.Parameters.Contains("id"))
            command.Parameters["id"].Value = id;
        else
            command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static async Task<bool> ExecuteDeleteAsync(string sql, string id)
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }
}
