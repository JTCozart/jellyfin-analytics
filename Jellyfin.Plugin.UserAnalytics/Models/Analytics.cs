using System;

namespace Jellyfin.Plugin.UserAnalytics.Models;

/// <summary>
/// Plays and watch time for a single calendar day.
/// </summary>
public class DailyActivityEntry
{
    /// <summary>
    /// Gets or sets the day (UTC, date component only).
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the number of plays on that day.
    /// </summary>
    public long PlayCount { get; set; }

    /// <summary>
    /// Gets or sets the total watch time on that day, in seconds.
    /// </summary>
    public long TotalPlaySeconds { get; set; }
}

/// <summary>
/// Plays and watch time grouped by item type.
/// </summary>
public class TypeActivityEntry
{
    /// <summary>
    /// Gets or sets the item type (Movie, Episode, Audio, ...).
    /// </summary>
    public string ItemType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of plays of that type.
    /// </summary>
    public long PlayCount { get; set; }

    /// <summary>
    /// Gets or sets the total watch time for that type, in seconds.
    /// </summary>
    public long TotalPlaySeconds { get; set; }
}

/// <summary>
/// Server-wide totals shown in the dashboard widgets.
/// </summary>
public class OverviewStats
{
    /// <summary>
    /// Gets or sets the total number of recorded plays.
    /// </summary>
    public long TotalPlays { get; set; }

    /// <summary>
    /// Gets or sets the number of users with activity.
    /// </summary>
    public long TotalUsers { get; set; }

    /// <summary>
    /// Gets or sets the total watch time across all users, in seconds.
    /// </summary>
    public long TotalPlaySeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of distinct items played.
    /// </summary>
    public long DistinctItems { get; set; }
}

/// <summary>
/// Plays and watch time grouped by play method (DirectPlay, Transcode, DirectStream, etc.).
/// </summary>
public class PlayMethodEntry
{
    /// <summary>
    /// Gets or sets the play method label.
    /// </summary>
    public string PlayMethod { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of plays using this method.
    /// </summary>
    public long PlayCount { get; set; }

    /// <summary>
    /// Gets or sets the total watch time for this method, in seconds.
    /// </summary>
    public long TotalPlaySeconds { get; set; }
}

/// <summary>
/// Result of a historical import from Jellyfin's per-user watch history.
/// </summary>
public class ImportResult
{
    /// <summary>
    /// Gets or sets the number of users scanned.
    /// </summary>
    public int UsersScanned { get; set; }

    /// <summary>
    /// Gets or sets the number of playback rows imported.
    /// </summary>
    public int RecordsImported { get; set; }

    /// <summary>
    /// Gets or sets a human-readable message describing the outcome.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
