using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.UserAnalytics.Data;
using Jellyfin.Plugin.UserAnalytics.Models;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserAnalytics.Services;

/// <summary>
/// Background service that records playback activity from live session events.
/// </summary>
public sealed class PlaybackTrackingService : IHostedService
{
    private readonly ISessionManager _sessionManager;
    private readonly IPlaybackRepository _repository;
    private readonly ILogger<PlaybackTrackingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackTrackingService"/> class.
    /// </summary>
    /// <param name="sessionManager">Instance of the <see cref="ISessionManager"/> interface.</param>
    /// <param name="repository">Instance of the <see cref="IPlaybackRepository"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{T}"/> interface.</param>
    public PlaybackTrackingService(
        ISessionManager sessionManager,
        IPlaybackRepository repository,
        ILogger<PlaybackTrackingService> logger)
    {
        _sessionManager = sessionManager;
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _logger.LogInformation("User Analytics playback tracking started");

        // Apply retention on startup.
        var config = Plugin.Instance?.Configuration;
        if (config is not null)
        {
            _repository.PruneOlderThan(config.RetentionDays);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _logger.LogInformation("User Analytics playback tracking stopped");
        return Task.CompletedTask;
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        try
        {
            var config = Plugin.Instance?.Configuration;
            if (config is { TrackLivePlayback: false })
            {
                return;
            }

            var item = e.Item;
            if (item is null || e.Users is null || e.Users.Count == 0)
            {
                return;
            }

            var playedSeconds = GetPlayedSeconds(e);
            var minimum = config?.MinimumPlaySeconds ?? 0;
            if (playedSeconds < minimum)
            {
                return;
            }

            var seriesName = item is Episode episode ? episode.SeriesName ?? string.Empty : string.Empty;
            var itemName = item.Name ?? string.Empty;
            var itemType = item.GetBaseItemKind().ToString();
            var clientName = e.ClientName ?? string.Empty;
            var deviceName = e.DeviceName ?? string.Empty;
            var playMethod = e.Session?.PlayState?.PlayMethod?.ToString() ?? string.Empty;

            foreach (var user in e.Users)
            {
                _repository.AddRecord(new PlaybackRecord
                {
                    UserId = user.Id,
                    UserName = user.Username ?? string.Empty,
                    ItemId = item.Id,
                    ItemName = itemName,
                    ItemType = itemType,
                    SeriesName = seriesName,
                    ClientName = clientName,
                    DeviceName = deviceName,
                    PlayMethod = playMethod,
                    PlayDurationSeconds = playedSeconds,
                    DateCreated = DateTime.UtcNow,
                });
            }

            _logger.LogDebug(
                "Recorded play of {ItemName} ({Seconds}s) for {UserCount} user(s)",
                itemName,
                playedSeconds,
                e.Users.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record playback activity");
        }
    }

    private static long GetPlayedSeconds(PlaybackStopEventArgs e)
    {
        if (e.PlaybackPositionTicks is { } ticks && ticks > 0)
        {
            return ticks / TimeSpan.TicksPerSecond;
        }

        return 0;
    }
}
