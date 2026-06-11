using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.UserAnalytics.Data;
using Jellyfin.Plugin.UserAnalytics.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UserAnalytics.Services;

/// <summary>
/// Best-effort import of historical playback activity from the server log files.
/// </summary>
/// <remarks>
/// Jellyfin's log format is not a stable API and does not always carry the user or item id,
/// so imported rows are attributed to a placeholder user when no user name is present in the
/// line. Re-running the import replaces previously imported rows rather than duplicating them.
/// </remarks>
public sealed class LogImportService
{
    /// <summary>
    /// Synthetic user id used for plays parsed from logs that have no associated user.
    /// </summary>
    public static readonly Guid UnknownUserId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    private const string ImportSource = "log";

    // Matches the timestamp at the start of a Jellyfin log line, e.g.
    // "[2024-01-15 10:30:00.123 +00:00] [INF] ...".
    private static readonly Regex TimestampRegex = new(
        @"^\[(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})",
        RegexOptions.Compiled);

    private readonly IApplicationPaths _applicationPaths;
    private readonly IPlaybackRepository _repository;
    private readonly ILogger<LogImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogImportService"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="repository">Instance of the <see cref="IPlaybackRepository"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{T}"/> interface.</param>
    public LogImportService(
        IApplicationPaths applicationPaths,
        IPlaybackRepository repository,
        ILogger<LogImportService> logger)
    {
        _applicationPaths = applicationPaths;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Scans the server log directory and imports any playback events found.
    /// </summary>
    /// <returns>A summary of the import.</returns>
    public LogImportResult Import()
    {
        var result = new LogImportResult();

        var pattern = Plugin.Instance?.Configuration.LogPlaybackPattern;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            result.Message = "No log playback pattern is configured.";
            return result;
        }

        Regex lineRegex;
        try
        {
            lineRegex = new Regex(pattern, RegexOptions.Compiled);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid log playback pattern");
            result.Message = "The configured log playback pattern is not a valid regular expression.";
            return result;
        }

        var logDir = _applicationPaths.LogDirectoryPath;
        if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
        {
            result.Message = "The server log directory could not be found.";
            return result;
        }

        var records = new List<PlaybackRecord>();
        foreach (var file in Directory.EnumerateFiles(logDir, "*.log", SearchOption.TopDirectoryOnly))
        {
            result.FilesScanned++;
            try
            {
                ParseFile(file, lineRegex, records);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not read log file {File}", file);
            }
        }

        result.RecordsImported = _repository.ReplaceImported(records, ImportSource);
        result.Message = result.RecordsImported > 0
            ? $"Imported {result.RecordsImported} play(s) from {result.FilesScanned} log file(s)."
            : $"Scanned {result.FilesScanned} log file(s) but found no matching playback lines. " +
              "The default log level may not record playback, or the pattern needs adjusting.";

        _logger.LogInformation("{Message}", result.Message);
        return result;
    }

    private static void ParseFile(string file, Regex lineRegex, List<PlaybackRecord> records)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var match = lineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var itemName = match.Groups["item"].Success ? match.Groups["item"].Value.Trim() : null;
            if (string.IsNullOrEmpty(itemName))
            {
                continue;
            }

            var date = TryParseTimestamp(line);
            var seconds = 0L;
            if (match.Groups["ms"].Success
                && long.TryParse(match.Groups["ms"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            {
                seconds = ms / 1000;
            }

            var userName = match.Groups["user"].Success && match.Groups["user"].Value.Length > 0
                ? match.Groups["user"].Value.Trim()
                : "Imported (unknown user)";

            records.Add(new PlaybackRecord
            {
                UserId = UnknownUserId,
                UserName = userName,
                ItemId = Guid.Empty,
                ItemName = itemName,
                ItemType = "Unknown",
                SeriesName = string.Empty,
                ClientName = "Log import",
                DeviceName = string.Empty,
                PlayMethod = string.Empty,
                PlayDurationSeconds = seconds,
                DateCreated = date,
            });
        }
    }

    private static DateTime TryParseTimestamp(string line)
    {
        var match = TimestampRegex.Match(line);
        if (match.Success
            && DateTime.TryParseExact(
                match.Groups["ts"].Value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return DateTime.UtcNow;
    }
}
