using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.UserAnalytics.Data;
using Jellyfin.Plugin.UserAnalytics.Models;
using Jellyfin.Plugin.UserAnalytics.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.UserAnalytics.Api;

/// <summary>
/// REST API exposing per-user playback analytics. Admin only.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("UserAnalytics")]
[Produces("application/json")]
public class UserAnalyticsController : ControllerBase
{
    private readonly IPlaybackRepository _repository;
    private readonly WatchHistoryImportService _watchHistoryImportService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserAnalyticsController"/> class.
    /// </summary>
    /// <param name="repository">Instance of the <see cref="IPlaybackRepository"/> interface.</param>
    /// <param name="watchHistoryImportService">The watch history import service.</param>
    public UserAnalyticsController(IPlaybackRepository repository, WatchHistoryImportService watchHistoryImportService)
    {
        _repository = repository;
        _watchHistoryImportService = watchHistoryImportService;
    }

    /// <summary>
    /// Gets server-wide totals for the dashboard widgets.
    /// </summary>
    /// <returns>The overview stats.</returns>
    [HttpGet("Overview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<OverviewStats> GetOverview()
        => Ok(_repository.GetOverview());

    /// <summary>
    /// Gets per-user summaries.
    /// </summary>
    /// <returns>The per-user summaries.</returns>
    [HttpGet("Users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<UserStats>> GetUsers()
        => Ok(_repository.GetUserSummaries());

    /// <summary>
    /// Gets detailed stats for a single user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The user's stats.</returns>
    [HttpGet("Users/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<UserStats> GetUser([FromRoute] Guid userId)
    {
        var stats = _repository.GetUserStats(userId);
        return stats is null ? NotFound() : Ok(stats);
    }

    /// <summary>
    /// Gets a page of a user's play history.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="limit">Maximum rows to return.</param>
    /// <param name="offset">Rows to skip.</param>
    /// <returns>The history page.</returns>
    [HttpGet("Users/{userId}/History")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PlayHistoryEntry>> GetHistory(
        [FromRoute] Guid userId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
        => Ok(_repository.GetPlayHistory(userId, Math.Clamp(limit, 1, 500), Math.Max(offset, 0)));

    /// <summary>
    /// Gets the most-played items server-wide, or scoped to a single user.
    /// </summary>
    /// <param name="userId">The user id, or empty for all users.</param>
    /// <param name="limit">Maximum items to return.</param>
    /// <returns>The top items.</returns>
    [HttpGet("TopItems")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TopItemEntry>> GetTopItems(
        [FromQuery] Guid? userId = null,
        [FromQuery] int limit = 10)
        => Ok(_repository.GetTopItems(Normalize(userId), Math.Clamp(limit, 1, 100)));

    /// <summary>
    /// Gets a user's most-played items.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="limit">Maximum items to return.</param>
    /// <returns>The top items.</returns>
    [HttpGet("Users/{userId}/TopItems")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TopItemEntry>> GetUserTopItems(
        [FromRoute] Guid userId,
        [FromQuery] int limit = 10)
        => Ok(_repository.GetTopItems(userId, Math.Clamp(limit, 1, 100)));

    /// <summary>
    /// Gets per-day activity for charts. Supply either <paramref name="from"/>/<paramref name="to"/>
    /// for an explicit range, or <paramref name="days"/> for a rolling lookback.
    /// </summary>
    /// <param name="userId">The user id, or empty for all users.</param>
    /// <param name="days">Rolling lookback in days (used when from/to are omitted).</param>
    /// <param name="from">Start date (UTC, inclusive). Overrides days when provided.</param>
    /// <param name="to">End date (UTC, inclusive). Defaults to today when from is provided.</param>
    /// <returns>The daily activity.</returns>
    [HttpGet("Timeline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DailyActivityEntry>> GetTimeline(
        [FromQuery] Guid? userId = null,
        [FromQuery] int days = 30,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null)
    {
        var end = DateTime.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedTo)
            ? parsedTo.Date.AddDays(1)
            : DateTime.UtcNow.Date.AddDays(1);

        var start = DateTime.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedFrom)
            ? parsedFrom.Date
            : end.AddDays(-Math.Clamp(days, 1, 365));

        return Ok(_repository.GetDailyActivity(Normalize(userId), start, end));
    }

    /// <summary>
    /// Gets activity grouped by item type for charts. Omit the user id for all users.
    /// </summary>
    /// <param name="userId">The user id, or empty for all users.</param>
    /// <returns>The per-type activity.</returns>
    [HttpGet("ByType")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TypeActivityEntry>> GetByType([FromQuery] Guid? userId = null)
        => Ok(_repository.GetActivityByType(Normalize(userId)));

    /// <summary>
    /// Gets activity grouped by play method (DirectPlay, Transcode, etc.). Omit the user id for all users.
    /// </summary>
    /// <param name="userId">The user id, or empty for all users.</param>
    /// <returns>The per-method activity.</returns>
    [HttpGet("ByPlayMethod")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PlayMethodEntry>> GetByPlayMethod([FromQuery] Guid? userId = null)
        => Ok(_repository.GetActivityByPlayMethod(Normalize(userId)));

    /// <summary>
    /// Imports historical playback from Jellyfin's per-user watch history (play counts and
    /// last-played dates). Watch time for imported plays is estimated from item runtime.
    /// </summary>
    /// <returns>A summary of the import.</returns>
    [HttpPost("Import/WatchHistory")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ImportResult> ImportWatchHistory()
        => Ok(_watchHistoryImportService.Import());

    private static Guid? Normalize(Guid? userId)
        => userId is null || userId.Value == Guid.Empty ? null : userId;
}
