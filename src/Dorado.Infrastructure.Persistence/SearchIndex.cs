using Microsoft.EntityFrameworkCore;

namespace Dorado.Infrastructure.Persistence;

/// <summary>
/// Manages the SQLite FTS5 full-text index over <c>Tracks</c>. The index is an
/// external-content table kept in sync by triggers, so it adds no duplicate storage.
/// </summary>
public static class SearchIndex
{
    private const string TableName = "TrackSearch";

    public static void Ensure(AppDbContext context)
    {
        var existed = TableExists(context);

        context.Database.ExecuteSqlRaw($"""
            CREATE VIRTUAL TABLE IF NOT EXISTS {TableName}
            USING fts5(Title, ArtistName, AlbumTitle, Genre, content='Tracks');
            """);

        context.Database.ExecuteSqlRaw($"""
            CREATE TRIGGER IF NOT EXISTS Tracks_fts_ai AFTER INSERT ON Tracks BEGIN
                INSERT INTO {TableName}(rowid, Title, ArtistName, AlbumTitle, Genre)
                VALUES (new.rowid, new.Title, new.ArtistName, new.AlbumTitle, new.Genre);
            END;
            """);

        context.Database.ExecuteSqlRaw($"""
            CREATE TRIGGER IF NOT EXISTS Tracks_fts_ad AFTER DELETE ON Tracks BEGIN
                INSERT INTO {TableName}({TableName}, rowid, Title, ArtistName, AlbumTitle, Genre)
                VALUES ('delete', old.rowid, old.Title, old.ArtistName, old.AlbumTitle, old.Genre);
            END;
            """);

        context.Database.ExecuteSqlRaw($"""
            CREATE TRIGGER IF NOT EXISTS Tracks_fts_au AFTER UPDATE ON Tracks BEGIN
                INSERT INTO {TableName}({TableName}, rowid, Title, ArtistName, AlbumTitle, Genre)
                VALUES ('delete', old.rowid, old.Title, old.ArtistName, old.AlbumTitle, old.Genre);
                INSERT INTO {TableName}(rowid, Title, ArtistName, AlbumTitle, Genre)
                VALUES (new.rowid, new.Title, new.ArtistName, new.AlbumTitle, new.Genre);
            END;
            """);

        // Backfill only when the index was just created (triggers keep it current after).
        if (!existed)
        {
            context.Database.ExecuteSqlRaw($"INSERT INTO {TableName}({TableName}) VALUES('rebuild');");
        }
    }

    /// <summary>Converts a user query into an FTS5 MATCH expression (prefix terms ANDed).</summary>
    public static string? BuildMatchExpression(string query)
    {
        var normalized = new string(query
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ')
            .ToArray());

        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => $"{token}*")
            .ToArray();

        return tokens.Length == 0 ? null : string.Join(" AND ", tokens);
    }

    public static bool TableExists(AppDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type IN ('table','view') AND name = $name";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = TableName;
            command.Parameters.Add(parameter);
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }
        finally
        {
            if (wasClosed)
            {
                connection.Close();
            }
        }
    }
}
