# Jellyfin User Analytics Plugin

[![Build Plugin](https://github.com/JTCozart/jellyfin-analytics/actions/workflows/build.yaml/badge.svg)](https://github.com/JTCozart/jellyfin-analytics/actions/workflows/build.yaml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)

A Jellyfin server plugin that produces **per-user playback analytics** — play history,
play counts, total watch time and most-watched items — shown in an admin dashboard (with
summary widgets and charts) and exposed over a REST API.

## How it works

The plugin subscribes to Jellyfin's live `ISessionManager` playback events
(`PlaybackStopped`) and records each completed play into a local **SQLite** database in the
plugin's data folder (`<config>/data/useranalytics/useranalytics.db`).

> Live tracking captures plays from the moment the plugin is installed forward. For older
> activity, see [Historical log import](#historical-log-import) — a best-effort backfill
> parsed from the server logs.

## Features

- **Summary widgets** — total plays, total watch time, active users, distinct items.
- **Charts** — plays over time (line) and watch time by media type (doughnut), powered by
  Chart.js; plus a per-user plays-over-time chart.
- **Per-user drilldown** — play count, watch time, most-watched items and paged play
  history for each user.
- **Configurable** — retention window, a minimum-play-seconds threshold to ignore
  accidental plays, and a toggle for live tracking.
- **Historical log import** — best-effort backfill of plays parsed from the server logs.
- **REST API** under `/UserAnalytics` (admin only).

## API

All endpoints require an authenticated admin (policy `RequiresElevation`).

| Method | Route | Description |
| ------ | ----- | ----------- |
| GET  | `/UserAnalytics/Overview` | Server-wide totals (widgets) |
| GET  | `/UserAnalytics/Users` | Per-user summaries |
| GET  | `/UserAnalytics/Users/{userId}` | Detailed stats for one user |
| GET  | `/UserAnalytics/Users/{userId}/History?limit=&offset=` | Paged play history |
| GET  | `/UserAnalytics/Users/{userId}/TopItems?limit=` | Most-watched items |
| GET  | `/UserAnalytics/Timeline?userId=&days=` | Per-day plays/watch time (charts) |
| GET  | `/UserAnalytics/ByType?userId=` | Plays/watch time grouped by media type |
| POST | `/UserAnalytics/Import/Logs` | Trigger a historical log import |

## Historical log import

Jellyfin's log format is **not a stable API** and often does not include the user or item
id, so this import is best-effort:

- It scans `*.log` files in the server log directory and matches a configurable regular
  expression (set in the plugin settings). The pattern must expose a named group `item`
  (required) and may expose `ms` (played milliseconds) and `user`.
- Matches without a `user` group are attributed to a placeholder *"Imported (unknown
  user)"* account.
- Re-running the import **replaces** previously imported rows, so it is idempotent.

Run it from the plugin's settings page ("Import from logs now") or via
`POST /UserAnalytics/Import/Logs`. Live event tracking remains the reliable data source.

## Building

```sh
dotnet build -c Release
```

The plugin DLL is written to `Jellyfin.Plugin.UserAnalytics/bin/Release/net9.0/`. The plugin
compiles against `Microsoft.Data.Sqlite` but does **not** bundle it — Jellyfin 10.11 already
ships `Microsoft.Data.Sqlite`, `SQLitePCLRaw` and the native SQLite library, so the packaged
plugin is a single DLL with no extra dependencies.

## Installing locally

1. Build (above).
2. Copy `bin/Release/net9.0/Jellyfin.Plugin.UserAnalytics.dll` into a `UserAnalytics` folder
   under your server's `plugins` directory.
3. Restart Jellyfin and open **Dashboard → Plugins → User Analytics**.

## Packaging / releases

Continuous integration builds the plugin on every push/PR via
[`.github/workflows/build.yaml`](.github/workflows/build.yaml), which delegates to
Jellyfin's shared meta-plugins workflow.

To cut a release, publish a GitHub Release whose tag is the plugin version (e.g.
`1.0.0.0`). [`.github/workflows/release.yaml`](.github/workflows/release.yaml) uses
[JPRM](https://github.com/oddstr13/jellyfin-plugin-repository-manager) to package the
plugin and attaches the resulting `.zip` to the release.

## Requirements

- Jellyfin **10.11.x** (ABI `10.11.0.0`)
- .NET 9 SDK to build

## License

[MIT](./LICENSE)
