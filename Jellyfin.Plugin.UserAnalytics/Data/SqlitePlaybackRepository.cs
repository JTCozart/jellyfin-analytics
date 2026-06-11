using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.UserAnalytics.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserAnalytics.Data;

/// <summary>
/// SQLite-backed implementation of <see cref="IPlaybackRepository"/>.
/// </summary>
public sealed class SqlitePlaybackRepository : IPlaybackRepository, IDisposable
{
    private readonly ILogger<SqlitePlaybackRepository> _logger;
    private readonly object _lock = new();
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlitePlaybackRepository"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{T}"/> interface.</param>
    public SqlitePlaybackRepository(IApplicationPaths applicationPaths, ILogger<SqlitePlaybackRepository> logger)
    {
        _logger = logger;

        var dataDir = Path.Combine(applicationPaths.DataPath, "useranalytics");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "useranalytics.db");

        _connection = new SqliteConnection($"Data Source={dbPath};");
        _connection.Open();
        InitializeSchema();

        _logger.LogInformation("User Analytics database ready at {DbPath}", dbPath);
    }

    private void InitializeSchema()
    {
        const string Sql = @"
CREATE TABLE IF NOT EXISTS PlaybackActivity (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId              TEXT NOT NULL,
    UserName            TEXT NOT NULL,
    ItemId              TEXT NOT NULL,
    ItemName            TEXT NOT NULL,
    ItemType            TEXT NOT NULL,
    SeriesName          TEXT NOT NULL DEFAULT '',
    ClientName          TEXT NOT NULL DEFAULT '',
    DeviceName          TEXT NOT NULL DEFAULT '',
    PlayMethod          TEXT NOT NULL DEFAULT '',
    PlayDurationSeconds INTEGER NOT NULL DEFAULT 0,
    DateCreated         TEXT NOT NULL,
    Source              TEXT NOT NULL DEFAULT 'live'
);
CREATE INDEX IF NOT EXISTS IX_PlaybackActivity_User_Date
    ON PlaybackActivity (UserId, DateCreated);
CREATE INDEX IF NOT EXISTS IX_PlaybackActivity_User_Item
    ON PlaybackActivity (UserId, ItemId);";

        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = Sql;
            cmd.ExecuteNonQuery();
        }
    }

    /// <inheritdoc />
    public void AddRecord(PlaybackRecord record)
    {
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            InsertRecord(cmd, record, "live");
        }
    }

    /// <inheritdoc />
    public int ReplaceImported(IReadOnlyList<PlaybackRecord> records, string source)
    {
        lock (_lock)
        {
            using var transaction = _connection.BeginTransaction();

            using (var delete = _connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM PlaybackActivity WHERE Source = $source;";
                delete.Parameters.AddWithValue("$source", source);
                delete.ExecuteNonQuery();
            }

            var inserted = 0;
            foreach (var record in records)
            {
                using var cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                InsertRecord(cmd, record, source);
                inserted++;
            }

            transaction.Commit();
            return inserted;
        }
    }

    private static void InsertRecord(SqliteCommand cmd, PlaybackRecord record, string source)
    {
        cmd.CommandText = @"
INSERT INTO PlaybackActivity
    (UserId, UserName, ItemId, ItemName, ItemType, SeriesName, ClientName, DeviceName, PlayMethod, PlayDurationSeconds, DateCreated, Source)
VALUES
    ($userId, $userName, $itemId, $itemName, $itemType, $seriesName, $clientName, $deviceName, $playMethod, $duration, $date, $source);";
        cmd.Parameters.AddWithValue("$userId", record.UserId.ToString("N"));
        cmd.Parameters.AddWithValue("$userName", record.UserName);
        cmd.Parameters.AddWithValue("$itemId", record.ItemId.ToString("N"));
        cmd.Parameters.AddWithValue("$itemName", record.ItemName);
        cmd.Parameters.AddWithValue("$itemType", record.ItemType);
        cmd.Parameters.AddWithValue("$seriesName", record.SeriesName);
        cmd.Parameters.AddWithValue("$clientName", record.ClientName);
        cmd.Parameters.AddWithValue("$deviceName", record.DeviceName);
        cmd.Parameters.AddWithValue("$playMethod", record.PlayMethod);
        cmd.Parameters.AddWithValue("$duration", record.PlayDurationSeconds);
        cmd.Parameters.AddWithValue("$date", ToIso(record.DateCreated));
        cmd.Parameters.AddWithValue("$source", source);
        cmd.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public IReadOnlyList<UserStats> GetUserSummaries()
    {
        const string Sql = @"
SELECT UserId,
       MAX(UserName)               AS UserName,
       COUNT(*)                    AS PlayCount,
       COALESCE(SUM(PlayDurationSeconds), 0) AS TotalPlaySeconds,
       COUNT(DISTINCT ItemId)      AS DistinctItems,
       MAX(DateCreated)            AS LastPlayed
FROM PlaybackActivity
GROUP BY UserId
ORDER BY TotalPlaySeconds DESC;";

        var results = new List<UserStats>();
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = Sql;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(ReadUserStats(reader));
            }
        }

        return results;
    }

    /// <inheritdoc />
    public UserStats? GetUserStats(Guid userId)
    {
        const string Sql = @"
SELECT UserId,
       MAX(UserName)               AS UserName,
       COUNT(*)                    AS PlayCount,
       COALESCE(SUM(PlayDurationSeconds), 0) AS TotalPlaySeconds,
       COUNT(DISTINCT ItemId)      AS DistinctItems,
       MAX(DateCreated)            AS LastPlayed
FROM PlaybackActivity
WHERE UserId = $userId
GROUP BY UserId;";

        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = Sql;
            cmd.Parameters.AddWithValue("$userId", userId.ToString("N"));
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadUserStats(reader) : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PlayHistoryEntry> GetPlayHistory(Guid userId, int limit, int offset)
    {
        const string Sql = @"
SELECT ItemId, ItemName, ItemType, SeriesName, ClientName, PlayDurationSeconds, DateCreated
FROM PlaybackActivity
WHERE UserId = $userId
ORDER BY DateCreated DESC
LIMIT $limit OFFSET $offset;";

        var results = new List<PlayHistoryEntry>();
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = Sql;
            cmd.Parameters.AddWithValue("$userId", userId.ToString("N"));
            cmd.Parameters.AddWithValue("$limit", limit);
            cmd.Parameters.AddWithValue("$offset", offset);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new PlayHistoryEntry
                {
                    ItemId = ParseGuid(reader.GetString(0)),
                    ItemName = reader.GetString(1),
                    ItemType = reader.GetString(2),
                    SeriesName = reader.GetString(3),
                    ClientName = reader.GetString(4),
                    PlayDurationSeconds = reader.GetInt64(5),
                    DateCreated = ParseIso(reader.GetString(6)),
                });
            }
        }

        return results;
    }

    /// <inheritdoc />
    public IReadOnlyList<TopItemEntry> GetTopItems(Guid userId, int limit)
    {
        const string Sql = @"
SELECT ItemId,
       MAX(ItemName)   AS ItemName,
       MAX(ItemType)   AS ItemType,
       MAX(SeriesName) AS SeriesName,
       COUNT(*)        AS PlayCount,
       COALESCE(SUM(PlayDurationSeconds), 0) AS TotalPlaySeconds
FROM PlaybackActivity
WHERE UserId = $userId
GROUP BY ItemId
ORDER BY PlayCount DESC, TotalPlaySeconds DESC
LIMIT $limit;";

        var results = new List<TopItemEntry>();
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = Sql;
            cmd.Parameters.AddWithValue("$userId", userId.ToString("N"));
            cmd.Parameters.AddWithValue("$limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new TopItemEntry
                {
                    ItemId = ParseGuid(reader.GetString(0)),
                    ItemName = reader.GetString(1),
                    ItemType = reader.GetString(2),
                    SeriesName = reader.GetString(3),
                    PlayCount = reader.GetInt64(4),
                    TotalPlaySeconds = reader.GetInt64(5),
                });
            }
        }

        return results;
    }

    /// <inheritdoc />
    public OverviewStats GetOverview()
    {
        const string Sql = @"
SELECT COUNT(*)                              AS TotalPlays,
       COUNT(DISTINCT UserId)                AS TotalUsers,
       COALESCE(SUM(PlayDurationSeconds), 0) AS TotalPlaySeconds,
       COUNT(DISTINCT ItemId)                AS DistinctItems
FROM PlaybackActivity;";

        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = Sql;
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new OverviewStats();
            }

            return new OverviewStats
            {
                TotalPlays = reader.GetInt64(0),
                TotalUsers = reader.GetInt64(1),
                TotalPlaySeconds = reader.GetInt64(2),
                DistinctItems = reader.GetInt64(3),
            };
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<DailyActivityEntry> GetDailyActivity(Guid? userId, int days)
    {
        if (days <= 0)
        {
            days = 30;
        }

        var sql = @"
SELECT substr(DateCreated, 1, 10) AS Day,
       COUNT(*)                    AS PlayCount,
       COALESCE(SUM(PlayDurationSeconds), 0) AS TotalPlaySeconds
FROM PlaybackActivity
WHERE DateCreated >= $cutoff" + (userId.HasValue ? " AND UserId = $userId" : string.Empty) + @"
GROUP BY Day
ORDER BY Day ASC;";

        var results = new List<DailyActivityEntry>();
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$cutoff", ToIso(DateTime.UtcNow.Date.AddDays(-days)));
            if (userId.HasValue)
            {
                cmd.Parameters.AddWithValue("$userId", userId.Value.ToString("N"));
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new DailyActivityEntry
                {
                    Date = DateTime.Parse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                    PlayCount = reader.GetInt64(1),
                    TotalPlaySeconds = reader.GetInt64(2),
                });
            }
        }

        return results;
    }

    /// <inheritdoc />
    public IReadOnlyList<TypeActivityEntry> GetActivityByType(Guid? userId)
    {
        var sql = @"
SELECT ItemType,
       COUNT(*)                    AS PlayCount,
       COALESCE(SUM(PlayDurationSeconds), 0) AS TotalPlaySeconds
FROM PlaybackActivity" + (userId.HasValue ? " WHERE UserId = $userId" : string.Empty) + @"
GROUP BY ItemType
ORDER BY TotalPlaySeconds DESC;";

        var results = new List<TypeActivityEntry>();
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            if (userId.HasValue)
            {
                cmd.Parameters.AddWithValue("$userId", userId.Value.ToString("N"));
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new TypeActivityEntry
                {
                    ItemType = reader.GetString(0),
                    PlayCount = reader.GetInt64(1),
                    TotalPlaySeconds = reader.GetInt64(2),
                });
            }
        }

        return results;
    }

    /// <inheritdoc />
    public void PruneOlderThan(int days)
    {
        if (days <= 0)
        {
            return;
        }

        var cutoff = ToIso(DateTime.UtcNow.AddDays(-days));
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM PlaybackActivity WHERE DateCreated < $cutoff;";
            cmd.Parameters.AddWithValue("$cutoff", cutoff);
            var removed = cmd.ExecuteNonQuery();
            if (removed > 0)
            {
                _logger.LogInformation("Pruned {Count} playback rows older than {Days} days", removed, days);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _connection.Dispose();
    }

    private static UserStats ReadUserStats(SqliteDataReader reader)
    {
        var lastPlayedRaw = reader.IsDBNull(5) ? null : reader.GetString(5);
        return new UserStats
        {
            UserId = ParseGuid(reader.GetString(0)),
            UserName = reader.GetString(1),
            PlayCount = reader.GetInt64(2),
            TotalPlaySeconds = reader.GetInt64(3),
            DistinctItems = reader.GetInt64(4),
            LastPlayed = lastPlayedRaw is null ? null : ParseIso(lastPlayedRaw),
        };
    }

    private static string ToIso(DateTime value)
        => value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ParseIso(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    private static Guid ParseGuid(string value)
        => Guid.TryParse(value, out var guid) ? guid : Guid.Empty;
}
