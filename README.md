# Jellyfin User Analytics Plugin

[![Build Plugin](https://github.com/JTCozart/jellyfin-analytics/actions/workflows/build.yaml/badge.svg)](https://github.com/JTCozart/jellyfin-analytics/actions/workflows/build.yaml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)
[![AI Generated Badge](https://www.aihonestybadge.com/badges/ai-generated.svg)](https://www.aihonestybadge.com)

A Jellyfin server plugin that produces **per-user playback analytics** — play history,
play counts, total watch time and most-watched items — shown in an admin dashboard (with
summary widgets and charts) and exposed over a REST API.

## How it works

The plugin subscribes to Jellyfin's live `ISessionManager` playback events
(`PlaybackStart` / `PlaybackStopped`) and records each completed play into a local **SQLite**
database in the plugin's data folder (`<config>/data/useranalytics/useranalytics.db`). Watch
time is measured as wall-clock time between start and stop, and each play session is recorded
exactly once (de-duplicated by play-session id).

> Live tracking captures plays from the moment the plugin is installed forward. For older
> activity, see [Importing watch history](#importing-watch-history) — a backfill from
> Jellyfin's own per-user watch history.

## Features

- **Summary widgets** — total plays, total watch time, active users, distinct items.
- **Charts** — plays over time (line) and watch time by media type (doughnut), powered by
  Chart.js; plus a per-user plays-over-time chart.
- **Per-user drilldown** — play count, watch time, most-watched items and paged play
  history for each user.
- **Configurable** — retention window, a minimum-play-seconds threshold to ignore
  accidental plays, and a toggle for live tracking.
- **Watch-history import** — backfill from Jellyfin's per-user watch history (play counts and
  last-played dates) so pre-install activity is included.
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
| POST | `/UserAnalytics/Import/WatchHistory` | Backfill from Jellyfin's watch history |

## Importing watch history

Jellyfin already records, per user and item, a **play count** and a **last-played date** (the
data behind "played" marks and resume). This import reads that history via `IUserDataManager`
so plays from before the plugin was installed are included.

- Run it from the dashboard ("Import watch history now") or via
  `POST /UserAnalytics/Import/WatchHistory`.
- Because the watch history does not store how long each individual play lasted, **watch time
  for imported plays is estimated from the item runtime** (runtime × play count). Live tracking
  remains the accurate source for watch time going forward.
- Re-running **replaces** previously imported rows, so it is idempotent.

## Building

```sh
dotnet build -c Release
```

The plugin DLL is written to `Jellyfin.Plugin.UserAnalytics/bin/Release/net9.0/`. The plugin
compiles against `Microsoft.Data.Sqlite` but does **not** bundle it — Jellyfin 10.11 already
ships `Microsoft.Data.Sqlite`, `SQLitePCLRaw` and the native SQLite library, so the packaged
plugin is a single DLL with no extra dependencies.

## Installing (recommended: plugin repository)

Add this repository in Jellyfin to install and get automatic updates from the catalog:

1. **Dashboard → Plugins → Repositories → ＋** and add the manifest URL:
   ```
   https://raw.githubusercontent.com/JTCozart/jellyfin-analytics/master/manifest.json
   ```
2. **Catalog →** find **User Analytics** (General) and click **Install**.
3. Restart Jellyfin, then open **User Analytics** from the Dashboard menu.

The full install guide with screenshots lives at
**https://jtcozart.github.io/jellyfin-analytics/**.

## Installing manually

1. Build (above), or download the `.zip` from [Releases](https://github.com/JTCozart/jellyfin-analytics/releases).
2. Copy `Jellyfin.Plugin.UserAnalytics.dll` into a `UserAnalytics` folder under your server's
   `plugins` directory.
3. Restart Jellyfin and open **Dashboard → Plugins → User Analytics**.

## Packaging / releases

Continuous integration builds the plugin on every push/PR via
[`.github/workflows/build.yaml`](.github/workflows/build.yaml), which delegates to
Jellyfin's shared meta-plugins workflow.

To cut a release, push a **timestamp tag** of the form `vYYYYMMDD.HHMM`:

```sh
git tag v$(date +%Y%m%d.%H%M)
git push origin --tags
```

[`.github/workflows/release.yaml`](.github/workflows/release.yaml) maps the tag to a 4-part
Jellyfin plugin version `YYYY.M.D.HHMM` (e.g. `v20260611.1430` → `2026.6.11.1430`). Because
the version is the timestamp, each release is strictly newer than the last, so Jellyfin
always recognizes it as an update. The workflow uses
[JPRM](https://github.com/oddstr13/jellyfin-plugin-repository-manager) to package the plugin,
creates a GitHub Release for the tag and attaches the resulting `.zip`.

## Requirements

- Jellyfin **10.11.x** (ABI `10.11.0.0`)
- .NET 9 SDK to build

## License

[MIT](./LICENSE)
