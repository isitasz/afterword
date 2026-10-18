using System;
using System.Linq;
using System.Threading.Tasks;
using MediaManager.Models;
using Npgsql;

namespace MediaManager.Data;

public sealed class BookRepository
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS books (
            database_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            id TEXT NOT NULL,
            title TEXT NOT NULL,
            author TEXT NOT NULL DEFAULT '',
            year TEXT NOT NULL DEFAULT '',
            rating DOUBLE PRECISION NOT NULL,
            date_read TEXT NOT NULL DEFAULT '',
            physical BOOLEAN NOT NULL,
            tags TEXT[] NOT NULL DEFAULT ARRAY[]::TEXT[],
            review TEXT NOT NULL DEFAULT '',
            cover TEXT NOT NULL DEFAULT '',
            cover_data BYTEA,
            cover_content_type TEXT,
            created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        ALTER TABLE books ADD COLUMN IF NOT EXISTS cover_data BYTEA;
        ALTER TABLE books ADD COLUMN IF NOT EXISTS cover_content_type TEXT;
        """;

    public async Task InitializeAsync()
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(CreateTableSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Book>> GetAllAsync()
    {
        const string sql = """
            SELECT id, title, author, year, rating, date_read, physical, tags, review,
                   cover_data IS NOT NULL
            FROM books
            ORDER BY database_id;
            """;

        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var books = new List<Book>();

        while (await reader.ReadAsync())
            books.Add(ReadBook(reader));

        return books;
    }

    public async Task<Book?> GetAsync(string id)
    {
        const string sql = """
            SELECT id, title, author, year, rating, date_read, physical, tags, review,
                   cover_data IS NOT NULL
            FROM books WHERE id=@id ORDER BY database_id LIMIT 1;
            """;

        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadBook(reader) : null;
    }

    public async Task<BookCover?> GetCoverAsync(string id, string? title)
    {
        const string sql = """
            SELECT cover_data, COALESCE(cover_content_type, 'image/jpeg')
            FROM books WHERE id=@id AND (@title IS NULL OR title=@title)
                AND cover_data IS NOT NULL
            ORDER BY database_id LIMIT 1;
            """;

        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.Add("title", NpgsqlTypes.NpgsqlDbType.Text).Value =
            title is null ? DBNull.Value : title;
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? new BookCover((byte[])reader[0], reader.GetString(1))
            : null;
    }

    public async Task<bool> UpdateAsync(string id, Book book)
    {
        const string sql = """
            UPDATE books SET title=@title, author=@author, year=@year, rating=@rating,
                date_read=@date_read, physical=@physical, tags=@tags, review=@review
            WHERE id=@id;
            """;

        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("title", book.Title);
        command.Parameters.AddWithValue("author", book.Author);
        command.Parameters.AddWithValue("year", book.Year);
        command.Parameters.AddWithValue("rating", book.Rating);
        command.Parameters.AddWithValue("date_read", book.DateRead);
        command.Parameters.AddWithValue("physical", book.Physical);
        command.Parameters.AddWithValue("tags", book.Tags.ToArray());
        command.Parameters.AddWithValue("review", book.Review);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM books WHERE id=@id", connection);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> UpdateCoverAsync(
        string id,
        string title,
        byte[] coverData,
        string coverContentType)
    {
        const string sql = """
            UPDATE books SET cover_data=@cover_data, cover_content_type=@content_type
            WHERE id=@id AND title=@title AND cover_data IS NULL;
            """;
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("cover_data", NpgsqlTypes.NpgsqlDbType.Bytea).Value = coverData;
        command.Parameters.AddWithValue("content_type", coverContentType);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task InsertAsync(
        Book book,
        byte[]? coverData,
        string? coverContentType)
    {
        await using var connection = Database.GetConnection();
        await connection.OpenAsync();

        await using (var createTable = new NpgsqlCommand(CreateTableSql, connection))
        {
            await createTable.ExecuteNonQueryAsync();
        }

        const string insertSql = """
            INSERT INTO books (
                id,
                title,
                author,
                year,
                rating,
                date_read,
                physical,
                tags,
                review,
                cover_data,
                cover_content_type
            ) VALUES (
                @id,
                @title,
                @author,
                @year,
                @rating,
                @date_read,
                @physical,
                @tags,
                @review,
                @cover_data,
                @cover_content_type
            );
            """;

        await using var insert = new NpgsqlCommand(insertSql, connection);
        insert.Parameters.AddWithValue("id", book.Id);
        insert.Parameters.AddWithValue("title", book.Title);
        insert.Parameters.AddWithValue("author", book.Author);
        insert.Parameters.AddWithValue("year", book.Year);
        insert.Parameters.AddWithValue("rating", book.Rating);
        insert.Parameters.AddWithValue("date_read", book.DateRead);
        insert.Parameters.AddWithValue("physical", book.Physical);
        insert.Parameters.AddWithValue("tags", book.Tags.ToArray());
        insert.Parameters.AddWithValue("review", book.Review);
        insert.Parameters.Add("cover_data", NpgsqlTypes.NpgsqlDbType.Bytea).Value =
            coverData is null ? DBNull.Value : coverData;
        insert.Parameters.Add("cover_content_type", NpgsqlTypes.NpgsqlDbType.Text).Value =
            coverContentType is null ? DBNull.Value : coverContentType;

        await insert.ExecuteNonQueryAsync();
    }

    private static Book ReadBook(NpgsqlDataReader reader)
    {
        var id = reader.GetString(0);
        var title = reader.GetString(1);
        return new Book
        {
            Id = id,
            Title = title,
            Author = reader.GetString(2),
            Year = reader.GetString(3),
            Rating = reader.GetDouble(4),
            DateRead = reader.GetString(5),
            Physical = reader.GetBoolean(6),
            Tags = reader.GetFieldValue<string[]>(7).ToList(),
            Review = reader.GetString(8),
            Cover = reader.GetBoolean(9)
                ? $"/api/books/{Uri.EscapeDataString(id)}/cover?title={Uri.EscapeDataString(title)}"
                : ""
        };
    }
}

public sealed record BookCover(byte[] Data, string ContentType);
