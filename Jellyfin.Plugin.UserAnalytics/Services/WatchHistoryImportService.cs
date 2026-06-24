using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.UserAnalytics.Data;
using Jellyfin.Plugin.UserAnalytics.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserAnalytics.Services;

/// <summary>
/// Backfills analytics from Jellyfin's own per-user watch history.
/// </summary>
/// <remarks>
/// Jellyfin records, per user and item, a play count and a last-played date (the data behind
/// "played" ticks and resume). This import reads that history via <see cref="IUserDataManager"/>
/// so activity from before the plugin was installed is included. Because the watch history does
/// not store how long each individual play lasted, watch time is estimated from the item runtime
/// (runtime x play count). Re-running replaces previously imported rows, so it is idempotent.
/// </remarks>
public sealed class WatchHistoryImportService
{
    private const string ImportSource = "history";

    private static readonly BaseItemKind[] PlayableKinds =
    {
        BaseItemKind.Movie,
        BaseItemKind.Episode,
        BaseItemKind.Audio,
        BaseItemKind.MusicVideo,
    };

    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IPlaybackRepository _repository;
    private readonly ILogger<WatchHistoryImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchHistoryImportService"/> class.
    /// </summary>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="userDataManager">Instance of the <see cref="IUserDataManager"/> interface.</param>
    /// <param name="repository">Instance of the <see cref="IPlaybackRepository"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{T}"/> interface.</param>
    public WatchHistoryImportService(
        IUserManager userManager,
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IPlaybackRepository repository,
        ILogger<WatchHistoryImportService> logger)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Reads every user's watch history and imports it into the analytics store.
    /// </summary>
    /// <returns>A summary of the import.</returns>
    public ImportResult Import()
    {
        var records = new List<PlaybackRecord>();
        var usersScanned = 0;

        foreach (var user in EnumerateUsers())
        {
            usersScanned++;

            var query = new InternalItemsQuery(user)
            {
                IsPlayed = true,
                Recursive = true,
                IncludeItemTypes = PlayableKinds,
                EnableTotalRecordCount = false,
            };

            foreach (var item in _libraryManager.GetItemList(query))
            {
                var data = _userDataManager.GetUserData(user, item);
                var playCount = data?.PlayCount ?? 0;
                if (playCount <= 0)
                {
                    continue;
                }

                // Guard against absurd values; one row per play so play counts stay accurate.
                playCount = Math.Min(playCount, 1000);

                var seriesName = item is Episode episode ? episode.SeriesName ?? string.Empty : string.Empty;
                var runtimeSeconds = item.RunTimeTicks.HasValue
                    ? item.RunTimeTicks.Value / TimeSpan.TicksPerSecond
                    : 0;
                var lastPlayed = data?.LastPlayedDate ?? DateTime.UtcNow;

                for (var i = 0; i < playCount; i++)
                {
                    records.Add(new PlaybackRecord
                    {
                        UserId = user.Id,
                        UserName = user.Username ?? string.Empty,
                        ItemId = item.Id,
                        ItemName = item.Name ?? string.Empty,
                        ItemType = item.GetBaseItemKind().ToString(),
                        SeriesName = seriesName,
                        ClientName = "Watch history",
                        DeviceName = string.Empty,
                        PlayMethod = string.Empty,
                        PlayDurationSeconds = runtimeSeconds,
                        DateCreated = lastPlayed,
                    });
                }
            }
        }

        var imported = _repository.ReplaceImported(records, ImportSource);

        var result = new ImportResult
        {
            UsersScanned = usersScanned,
            RecordsImported = imported,
            Message = imported > 0
                ? $"Imported {imported} play(s) from the watch history of {usersScanned} user(s). " +
                  "Watch time for imported plays is estimated from item runtime."
                : $"Scanned {usersScanned} user(s) but found no played items in the watch history.",
        };

        _logger.LogInformation("{Message}", result.Message);
        return result;
    }

    /// <summary>
    /// Enumerates all users in a way that works across Jellyfin 10.11.x releases.
    /// </summary>
    /// <remarks>
    /// 10.11.7 exposes the user list via an <c>IUserManager.Users</c> property; 10.11.11 replaced
    /// it with a <c>GetUsers()</c> method. The plugin is compiled against 10.11.7, so a direct call
    /// to the property throws <see cref="MissingMethodException"/> on 10.11.11. Resolving the member
    /// by reflection lets the single build run on both server versions.
    /// </remarks>
    private IEnumerable<User> EnumerateUsers()
    {
        var managerType = _userManager.GetType();
        var users = managerType.GetMethod("GetUsers", Type.EmptyTypes)?.Invoke(_userManager, null)
            ?? managerType.GetProperty("Users")?.GetValue(_userManager);

        return users as IEnumerable<User> ?? Array.Empty<User>();
    }
}
