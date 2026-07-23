<p align="center">
  <img src="assets/banner.png" alt="Jellygram" width="800">
</p>

<h1 align="center">Jellygram</h1>

<p align="center">
  Rich, grouped Telegram notifications for newly added Jellyfin movies and TV.
</p>

<p align="center">
  <a href="https://github.com/TheCommishDeuce/jellyfin-plugin-jellygram/actions/workflows/ci.yml"><img src="https://github.com/TheCommishDeuce/jellyfin-plugin-jellygram/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/Jellyfin-12.0.0--rc2-00A4DC" alt="Jellyfin 12.0.0-rc2">
</p>

Jellygram runs inside Jellyfin and listens directly for library additions. It waits for bulk imports to settle, resolves their hierarchy, and sends concise Telegram updates without requiring a webhook, sidecar service, or public endpoint.

## Features

- Notifications for movies, series, seasons, and episodes
- Posters and descriptions from Jellyfin metadata
- Hierarchical grouping that avoids one message per episode
- Sliding batch window for slow and staggered imports
- Metadata-readiness retries for episodes still being identified
- Telegram groups, channels, and forum topics
- Optional silent delivery
- Persistent configuration through the Jellyfin dashboard
- HTML escaping and delivery retries

## Notification behavior

| Jellyfin addition | Telegram result |
|---|---|
| Movie | Movie poster, title, year, and overview |
| Entire new series | One show digest with season and episode counts |
| New season on an existing series | One season digest that absorbs its episodes |
| One episode on an existing season | Episode code, title, overview, and artwork |
| Multiple episodes | One grouped digest with ranges such as `S04: E01–E03, E05` |

Higher-level additions take precedence. Importing a new series does not also produce separate season and episode notifications.

## Compatibility

The `1.0.0.0` release targets:

- Jellyfin `12.0.0-rc2`
- .NET `10.0`

Jellyfin plugins are ABI-specific. Do not install this build on Jellyfin 10.10 or 10.11. A new plugin release may be required when Jellyfin 12 moves from an RC to a stable ABI.

## Installation

### Plugin repository

After the first GitHub release, add this URL under **Dashboard → Plugins → Repositories**:

```text
https://raw.githubusercontent.com/TheCommishDeuce/jellyfin-plugin-jellygram/main/manifest.json
```

Then install **Jellygram** from the plugin catalog and restart Jellyfin.

### Manual Docker installation

1. Download `Jellygram_1.0.0.0.zip` from the GitHub release.
2. Extract `Jellyfin.Plugin.Jellygram.dll` into the Jellyfin configuration volume:

   ```text
   /config/plugins/Jellygram/Jellyfin.Plugin.Jellygram.dll
   ```

3. Restart the Jellyfin container.

For development deployments from this repository:

```bash
./scripts/deploy-docker.sh root@your-jellyfin-host
```

The script builds locally, finds the running Jellyfin container, copies the DLL, and restarts the container.

## Telegram setup

1. Message [@BotFather](https://t.me/BotFather), run `/newbot`, and copy the bot token.
2. Add the bot to the destination group or channel. For a channel, grant permission to post messages.
3. Send a message in the destination chat.
4. Open `https://api.telegram.org/bot<TOKEN>/getUpdates` and find `message.chat.id` or `channel_post.chat.id`.
5. For forum topics, use the `message_thread_id` from an update posted inside the topic.

Treat the bot token like a password. If it is exposed, rotate it with BotFather.

## Configuration

Open **Dashboard → Plugins → My Plugins → Jellygram**.

| Setting | Description |
|---|---|
| Telegram bot token | Token issued by BotFather |
| Chat ID | User, group, supergroup, or channel ID; group IDs are commonly negative |
| Topic/thread ID | Optional Telegram forum topic |
| Batch window | Seconds of inactivity before sending; every new item resets the timer |
| Include descriptions | Uses Jellyfin's `Overview` metadata |
| Include posters | Uploads Jellyfin's primary image when Telegram's caption limit permits |
| Send silently | Delivers without a notification sound |

A window of `90` seconds is suitable for normal imports. For large 4K seasons copied sequentially, `300`–`600` seconds is safer.

## How batching works

Jellygram starts an inactivity timer when Jellyfin emits `ItemAdded`. Additional additions reset the timer. Once the window closes, Jellygram reloads the items from Jellyfin and builds the smallest useful set of notifications.

If an episode still lacks a series ID, season ID, season number, or episode number, the whole batch waits and retries metadata resolution every minute. After ten retries it sends what it can and logs a warning, preventing one malformed item from blocking notifications indefinitely.

Jellygram currently tracks newly created Jellyfin items. Adding a 4K alternate version to an episode that already exists may produce `ItemUpdated` rather than `ItemAdded` and is intentionally not notified.

## Logs

Jellygram uses Jellyfin's normal logging system. For Docker:

```bash
docker logs --follow jellyfin 2>&1 | grep --line-buffered -iE 'jellygram|telegram'
```

Successful batches look like:

```text
Jellyfin.Plugin.Jellygram.Notifications.LibraryNotifier: Sent 1 Telegram notifications for 14 library items
```

## Build and test

Requirements: .NET 10 SDK.

```bash
dotnet restore Jellyfin.Plugin.Jellygram.sln
dotnet test Jellyfin.Plugin.Jellygram.sln -c Release
dotnet build Jellyfin.Plugin.Jellygram.sln -c Release --no-restore
```

Create the release archive and MD5 checksum:

```bash
./scripts/package.sh
```

## Privacy and security

Jellygram sends the configured notification text and selected artwork to Telegram's Bot API. It does not expose an inbound HTTP endpoint or contact metadata providers directly. The bot token is stored in Jellyfin's plugin configuration and is never intentionally logged.

See [SECURITY.md](SECURITY.md) for vulnerability reporting.

## License

[MIT](LICENSE)
