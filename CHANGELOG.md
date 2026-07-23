# Changelog

All notable changes to Jellygram are documented here.

## 1.1.0.0 — 2026-07-23

- Show the imported season number in new-show digests, such as `Season 05 · 8 episodes`.
- Show compact ranges when multiple seasons are imported.
- Derive season numbers from episode metadata when no usable season parent event is available.
- Use a padded 16:9 image on Jellyfin's plugin details page.

## 1.0.0.0 — 2026-07-22

- Send Telegram notifications for newly added movies, series, seasons, and episodes.
- Collapse full-series imports into one show digest.
- Collapse season imports and episode batches into readable grouped notifications.
- Use a configurable sliding batch window to handle long-running library imports.
- Wait for incomplete TV hierarchy metadata before formatting a batch.
- Include Jellyfin overviews and primary artwork when available.
- Support Telegram groups, channels, forum topics, and silent delivery.
- Escape Telegram HTML and retry failed deliveries.
