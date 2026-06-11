using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.UserAnalytics.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        RetentionDays = 0;
        MinimumPlaySeconds = 30;
        TrackLivePlayback = true;
    }

    /// <summary>
    /// Gets or sets the number of days of history to keep. 0 keeps history forever.
    /// </summary>
    public int RetentionDays { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of played seconds required to record a play.
    /// Shorter plays (accidental clicks, quick skips) are ignored.
    /// </summary>
    public int MinimumPlaySeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether live playback events are tracked.
    /// </summary>
    public bool TrackLivePlayback { get; set; }
}
