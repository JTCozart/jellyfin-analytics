using System;

namespace Jellyfin.Plugin.UserAnalytics.Models;

/// <summary>
/// A single completed playback event for one user and item.
/// </summary>
public class PlaybackRecord
{
    /// <summary>
    /// Gets or sets the row id (assigned by the database).
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the user display name at the time of playback.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the played item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the played item name.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the played item type (Movie, Episode, Audio, ...).
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
    /// Gets or sets the device name.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the play method (DirectPlay, Transcode, ...).
    /// </summary>
    public string PlayMethod { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of seconds the item was played for.
    /// </summary>
    public long PlayDurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the play ended / was recorded.
    /// </summary>
    public DateTime DateCreated { get; set; }
}
