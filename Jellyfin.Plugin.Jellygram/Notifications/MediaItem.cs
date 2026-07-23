namespace Jellyfin.Plugin.Jellygram.Notifications;

public sealed record MediaItem(
    Guid Id,
    string Type,
    string Name,
    string Overview,
    int? Year,
    Guid? SeriesId,
    string? SeriesName,
    Guid? SeasonId,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? ImagePath);

public sealed record TelegramNotification(string Text, string? ImagePath);
