using System;

namespace Jellyfin.Plugin.UserAnalytics.Models;

/// <summary>
/// A single row of play history shown in the dashboard.
/// </summary>
public class PlayHistoryEntry
{
    /// <summary>
    /// Gets or sets the played item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the display label for the item (series + episode where relevant).
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type.
    /// </summary>
    public string ItemType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the series name for episodes (empty otherwise).
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client application name.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the play duration in seconds.
    /// </summary>
    public long PlayDurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the play was recorded.
    /// </summary>
    public DateTime DateCreated { get; set; }
}

/// <summary>
/// Aggregated play count for a single item (used for "top items").
/// </summary>
public class TopItemEntry
{
    /// <summary>
    /// Gets or sets the item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the display label for the item.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type.
    /// </summary>
    public string ItemType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the series name for episodes (empty otherwise).
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of times the item was played.
    /// </summary>
    public long PlayCount { get; set; }

    /// <summary>
    /// Gets or sets the total watch time in seconds for the item.
    /// </summary>
    public long TotalPlaySeconds { get; set; }
}
