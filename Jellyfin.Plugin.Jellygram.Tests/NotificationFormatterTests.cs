using Jellyfin.Plugin.Jellygram.Notifications;
using Xunit;

namespace Jellyfin.Plugin.Jellygram.Tests;

public sealed class NotificationFormatterTests
{
    [Fact]
    public void NewSeriesAbsorbsChildEvents()
    {
        var showId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var items = new[]
        {
            Item(showId, "Series", "Severance", overview: "Work-life balance."),
            Item(seasonId, "Season", "Season 2", showId, "Severance", seasonNumber: 2),
            Item(Guid.NewGuid(), "Episode", "Hello, Ms. Cobel", showId, "Severance", seasonId, 2, 1)
        };

        var notification = Assert.Single(NotificationFormatter.Build(items, true));
        Assert.Contains("New show", notification.Text);
        Assert.Contains("Season 02 · 1 episode", notification.Text);
        Assert.Contains("Work-life balance.", notification.Text);
    }

    [Fact]
    public void NewSeriesShowsLaterSeasonNumberFromEpisodes()
    {
        var showId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var items = new List<MediaItem> { Item(showId, "Series", "The Bear") };
        items.AddRange(Enumerable.Range(1, 8)
            .Select(number => Item(Guid.NewGuid(), "Episode", $"Episode {number}", showId, "The Bear", seasonId, 5, number)));

        var notification = Assert.Single(NotificationFormatter.Build(items, true));

        Assert.Contains("Season 05 · 8 episodes", notification.Text);
    }

    [Fact]
    public void EpisodesAreGroupedIntoRanges()
    {
        var showId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var items = new[] { 1, 2, 3, 5 }
            .Select(number => Item(Guid.NewGuid(), "Episode", $"Part {number}", showId, "Slow Horses", seasonId, 4, number));

        var notification = Assert.Single(NotificationFormatter.Build(items, true));
        Assert.Contains("New episodes", notification.Text);
        Assert.Contains("S04: E01–E03, E05", notification.Text);
    }

    [Fact]
    public void MetadataIsHtmlEncoded()
    {
        var notification = Assert.Single(NotificationFormatter.Build(
            [Item(Guid.NewGuid(), "Movie", "Alien & <Friends>", overview: "Space & beyond")], true));

        Assert.Contains("Alien &amp; &lt;Friends&gt;", notification.Text);
        Assert.Contains("Space &amp; beyond", notification.Text);
    }

    private static MediaItem Item(
        Guid id,
        string type,
        string name,
        Guid? seriesId = null,
        string? seriesName = null,
        Guid? seasonId = null,
        int? seasonNumber = null,
        int? episodeNumber = null,
        string overview = "") =>
        new(id, type, name, overview, null, seriesId, seriesName, seasonId, seasonNumber, episodeNumber, null);
}
