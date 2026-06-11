using System;

namespace Jellyfin.Plugin.UserAnalytics.Models;

/// <summary>
/// Aggregated playback statistics for a single user.
/// </summary>
public class UserStats
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the user display name.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of recorded plays.
    /// </summary>
    public long PlayCount { get; set; }

    /// <summary>
    /// Gets or sets the total watch time in seconds.
    /// </summary>
    public long TotalPlaySeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of distinct items played.
    /// </summary>
    public long DistinctItems { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the most recent play, if any.
    /// </summary>
    public DateTime? LastPlayed { get; set; }
}
