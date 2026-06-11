using System;
using System.Collections.Generic;
using Jellyfin.Plugin.UserAnalytics.Models;

namespace Jellyfin.Plugin.UserAnalytics.Data;

/// <summary>
/// Persists and queries playback activity.
/// </summary>
public interface IPlaybackRepository
{
    /// <summary>
    /// Records a completed play.
    /// </summary>
    /// <param name="record">The record to persist.</param>
    void AddRecord(PlaybackRecord record);

    /// <summary>
    /// Gets aggregated statistics for every user that has any recorded activity.
    /// </summary>
    /// <returns>The per-user summaries.</returns>
    IReadOnlyList<UserStats> GetUserSummaries();

    /// <summary>
    /// Gets aggregated statistics for a single user.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The user's stats, or <c>null</c> when there is no activity.</returns>
    UserStats? GetUserStats(Guid userId);

    /// <summary>
    /// Gets a page of a user's play history, most recent first.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="limit">Maximum rows to return.</param>
    /// <param name="offset">Number of rows to skip.</param>
    /// <returns>The history page.</returns>
    IReadOnlyList<PlayHistoryEntry> GetPlayHistory(Guid userId, int limit, int offset);

    /// <summary>
    /// Gets a user's most-played items.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="limit">Maximum items to return.</param>
    /// <returns>The top items.</returns>
    IReadOnlyList<TopItemEntry> GetTopItems(Guid userId, int limit);

    /// <summary>
    /// Gets server-wide totals.
    /// </summary>
    /// <returns>The overview stats.</returns>
    OverviewStats GetOverview();

    /// <summary>
    /// Gets per-day plays/watch time over the last <paramref name="days"/> days.
    /// </summary>
    /// <param name="userId">The user id, or <c>null</c> for all users.</param>
    /// <param name="days">Number of days to look back.</param>
    /// <returns>The daily activity, oldest first.</returns>
    IReadOnlyList<DailyActivityEntry> GetDailyActivity(Guid? userId, int days);

    /// <summary>
    /// Gets plays/watch time grouped by item type.
    /// </summary>
    /// <param name="userId">The user id, or <c>null</c> for all users.</param>
    /// <returns>The per-type activity.</returns>
    IReadOnlyList<TypeActivityEntry> GetActivityByType(Guid? userId);

    /// <summary>
    /// Bulk-inserts imported records, first removing any previously imported rows from the
    /// same source so re-imports are idempotent.
    /// </summary>
    /// <param name="records">The records to insert.</param>
    /// <param name="source">The source tag (e.g. <c>log</c>).</param>
    /// <returns>The number of rows inserted.</returns>
    int ReplaceImported(IReadOnlyList<PlaybackRecord> records, string source);

    /// <summary>
    /// Deletes records older than the given number of days. A value of 0 keeps everything.
    /// </summary>
    /// <param name="days">Retention window in days.</param>
    void PruneOlderThan(int days);
}
