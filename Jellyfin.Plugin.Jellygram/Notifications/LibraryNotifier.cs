using System.Collections.Concurrent;
using Jellyfin.Plugin.Jellygram.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellygram.Notifications;

public sealed class LibraryNotifier(
    ILibraryManager libraryManager,
    TelegramSender sender,
    ILogger<LibraryNotifier> logger) : BackgroundService
{
    private const int MaxMetadataRetries = 10;
    private readonly ConcurrentDictionary<Guid, byte> _queue = new();
    private long _lastAddedTicks;
    private int _metadataRetryCount;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        libraryManager.ItemAdded += ItemAdded;
        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        libraryManager.ItemAdded -= ItemAdded;
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            if (_queue.IsEmpty)
            {
                continue;
            }

            var configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration();
            var batchSeconds = Math.Clamp(configuration.BatchSeconds, 5, 3600);
            var lastAdded = new DateTime(Interlocked.Read(ref _lastAddedTicks), DateTimeKind.Utc);
            if (DateTime.UtcNow - lastAdded < TimeSpan.FromSeconds(batchSeconds))
            {
                continue;
            }

            await FlushAsync(configuration, stoppingToken).ConfigureAwait(false);
        }
    }

    private void ItemAdded(object? sender, ItemChangeEventArgs eventArgs)
    {
        if (eventArgs.Item.IsVirtualItem || eventArgs.Item is not (Movie or Series or Season or Episode))
        {
            return;
        }

        _queue.TryAdd(eventArgs.Item.Id, 0);
        Interlocked.Exchange(ref _lastAddedTicks, DateTime.UtcNow.Ticks);
        logger.LogDebug("Queued {ItemType} {ItemName} for Telegram", eventArgs.Item.GetType().Name, eventArgs.Item.Name);
    }

    private async Task FlushAsync(PluginConfiguration configuration, CancellationToken cancellationToken)
    {
        var ids = _queue.Keys.ToArray();
        try
        {
            var libraryItems = ids
                .Select(libraryManager.GetItemById)
                .Where(item => item is not null)
                .Cast<BaseItem>()
                .ToList();
            var incompleteItems = libraryItems.Where(IsMetadataIncomplete).ToList();
            if (incompleteItems.Count > 0 && _metadataRetryCount < MaxMetadataRetries)
            {
                _metadataRetryCount++;
                logger.LogInformation(
                    "Waiting for hierarchy metadata on {ItemCount} TV items before sending Jellygram batch (attempt {Attempt}/{MaxAttempts})",
                    incompleteItems.Count,
                    _metadataRetryCount,
                    MaxMetadataRetries);
                ScheduleRetry(configuration, 60);
                return;
            }

            if (incompleteItems.Count > 0)
            {
                logger.LogWarning(
                    "Sending Jellygram batch with {ItemCount} TV items still missing hierarchy metadata after {AttemptCount} retries",
                    incompleteItems.Count,
                    MaxMetadataRetries);
            }

            var items = libraryItems
                .Select(Convert)
                .Where(item => item is not null)
                .Cast<MediaItem>()
                .ToList();
            var notifications = NotificationFormatter.Build(items, configuration.IncludeDescriptions);
            foreach (var notification in notifications)
            {
                await sender.SendAsync(notification, cancellationToken).ConfigureAwait(false);
            }

            foreach (var id in ids)
            {
                _queue.TryRemove(id, out _);
            }

            _metadataRetryCount = 0;
            logger.LogInformation("Sent {NotificationCount} Telegram notifications for {ItemCount} library items", notifications.Count, items.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not send Jellygram batch; it will be retried");
            ScheduleRetry(configuration, 30);
        }
    }

    private void ScheduleRetry(PluginConfiguration configuration, int delaySeconds)
    {
        var batchSeconds = Math.Clamp(configuration.BatchSeconds, 5, 3600);
        Interlocked.Exchange(ref _lastAddedTicks, DateTime.UtcNow.AddSeconds(delaySeconds - batchSeconds).Ticks);
    }

    private static bool IsMetadataIncomplete(BaseItem item) => item switch
    {
        Season season => season.SeriesId == Guid.Empty || season.IndexNumber is null,
        Episode episode => episode.SeriesId == Guid.Empty
            || episode.SeasonId == Guid.Empty
            || episode.ParentIndexNumber is null
            || episode.IndexNumber is null,
        _ => false
    };

    private MediaItem? Convert(BaseItem item)
    {
        var imagePath = item.HasImage(ImageType.Primary) ? item.PrimaryImagePath : null;
        return item switch
        {
            Movie movie => New(movie, "Movie", null, null, null, null, null, imagePath),
            Series series => New(series, "Series", null, null, null, null, null, imagePath),
            Season season => New(
                season,
                "Season",
                season.SeriesId,
                season.Series?.Name ?? libraryManager.GetItemById(season.SeriesId)?.Name,
                null,
                season.IndexNumber,
                null,
                imagePath),
            Episode episode => New(
                episode,
                "Episode",
                episode.SeriesId,
                episode.Series?.Name ?? libraryManager.GetItemById(episode.SeriesId)?.Name,
                episode.SeasonId,
                episode.ParentIndexNumber ?? (libraryManager.GetItemById(episode.SeasonId) as Season)?.IndexNumber,
                episode.IndexNumber,
                imagePath),
            _ => null
        };
    }

    private static MediaItem New(
        BaseItem item,
        string type,
        Guid? seriesId,
        string? seriesName,
        Guid? seasonId,
        int? seasonNumber,
        int? episodeNumber,
        string? imagePath) =>
        new(item.Id, type, item.Name, item.Overview ?? string.Empty, item.ProductionYear, seriesId, seriesName, seasonId, seasonNumber, episodeNumber, imagePath);
}
