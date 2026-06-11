using System;
using System.Collections.Generic;
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
    /// Gets a user's most-played items.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="limit">Maximum items to return.</param>
    /// <returns>The top items.</returns>
    [HttpGet("Users/{userId}/TopItems")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<TopItemEntry>> GetTopItems(
        [FromRoute] Guid userId,
        [FromQuery] int limit = 10)
        => Ok(_repository.GetTopItems(userId, Math.Clamp(limit, 1, 100)));

    /// <summary>
    /// Gets per-day activity for charts. Omit the user id for all users.
    /// </summary>
    /// <param name="userId">The user id, or empty for all users.</param>
    /// <param name="days">Number of days to look back.</param>
    /// <returns>The daily activity.</returns>
    [HttpGet("Timeline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DailyActivityEntry>> GetTimeline(
        [FromQuery] Guid? userId = null,
        [FromQuery] int days = 30)
        => Ok(_repository.GetDailyActivity(Normalize(userId), Math.Clamp(days, 1, 365)));

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
