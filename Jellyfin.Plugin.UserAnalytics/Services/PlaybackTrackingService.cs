using System;
using System.Collections.Concurrent;
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
/// <remarks>
/// One row is recorded per play session. The watch duration is measured as wall-clock time
/// between <c>PlaybackStart</c> and <c>PlaybackStopped</c> (not the resume position, which
/// would over-count when a user skips to the end). De-duplication keys off the play session
/// id, because Jellyfin can raise <c>PlaybackStopped</c> more than once for a single play.
/// </remarks>
public sealed class PlaybackTrackingService : IHostedService
{
    private readonly ISessionManager _sessionManager;
    private readonly IPlaybackRepository _repository;
    private readonly ILogger<PlaybackTrackingService> _logger;

    // Key: play session id -> (when playback started UTC, play method captured at start).
    // PlayMethod is captured at PlaybackStart because the session's PlayState is cleared
    // before PlaybackStopped fires, leaving PlayMethod null if read at stop time.
    private readonly ConcurrentDictionary<string, (DateTime StartedUtc, string PlayMethod)> _inFlight = new(StringComparer.Ordinal);

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
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _logger.LogInformation("User Analytics playback tracking started");

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
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _inFlight.Clear();
        _logger.LogInformation("User Analytics playback tracking stopped");
        return Task.CompletedTask;
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        var key = GetSessionKey(e);
        if (key is not null)
        {
            var playMethod = e.Session?.PlayState?.PlayMethod?.ToString() ?? string.Empty;
            _inFlight[key] = (DateTime.UtcNow, playMethod);
        }
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

            var key = GetSessionKey(e);

            // Record only the first stop for a session. TryRemove returns false for a
            // duplicate stop (already recorded) or for a play whose start we never saw
            // (e.g. it began before the plugin loaded) - both are skipped, which keeps the
            // play count and the wall-clock duration correct.
            if (key is null || !_inFlight.TryRemove(key, out var inFlight))
            {
                return;
            }

            var playedSeconds = (long)Math.Max(0, (DateTime.UtcNow - inFlight.StartedUtc).TotalSeconds);

            if (config?.CapWatchTimeToRuntime == true && item.RunTimeTicks is > 0)
            {
                playedSeconds = Math.Min(playedSeconds, item.RunTimeTicks.Value / 10_000_000L);
            }

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
            var playMethod = inFlight.PlayMethod;

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

    private static string? GetSessionKey(PlaybackProgressEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PlaySessionId))
        {
            return e.PlaySessionId;
        }

        if (!string.IsNullOrEmpty(e.Session?.Id))
        {
            return e.Session.Id;
        }

        if (e.Item is null || e.Users is null || e.Users.Count == 0)
        {
            return null;
        }

        return $"{e.Users[0].Id:N}:{e.Item.Id:N}";
    }
}
