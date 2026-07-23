# Contributing

Issues and pull requests are welcome.

## Development

Requirements: .NET 10 SDK and a Jellyfin 12.0-compatible server for integration testing.

```bash
dotnet restore Jellyfin.Plugin.Jellygram.sln
dotnet test Jellyfin.Plugin.Jellygram.sln -c Release
dotnet build Jellyfin.Plugin.Jellygram.sln -c Release
```

Keep changes focused, add formatter tests for notification behavior, and never commit Telegram tokens or real chat IDs. Integration tests should use a private test bot and chat.
