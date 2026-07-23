# Security policy

## Reporting a vulnerability

Please report security issues privately through GitHub's **Security → Report a vulnerability** feature. Do not open a public issue containing a Telegram bot token, chat ID, server URL, logs with credentials, or exploit details.

## Credential handling

Jellygram stores its Telegram bot token in Jellyfin's plugin configuration directory. Protect the Jellyfin configuration volume and administrative account accordingly. The token is sent only to Telegram's Bot API and is never intentionally written to logs.
