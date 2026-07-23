using System.Net;
using System.Text;

namespace Jellyfin.Plugin.Jellygram.Notifications;

public static class NotificationFormatter
{
    public static IReadOnlyList<TelegramNotification> Build(IEnumerable<MediaItem> source, bool descriptions)
    {
        var items = source.DistinctBy(x => x.Id).ToList();
        var result = new List<TelegramNotification>();

        foreach (var movie in items.Where(x => x.Type == "Movie").OrderBy(x => x.Name))
        {
            result.Add(Create($"🎬 <b>New movie</b>\n<b>{E(movie.Name)}{Year(movie)}</b>", movie, descriptions));
        }

        var series = items.Where(x => x.Type == "Series").ToList();
        var seasons = items.Where(x => x.Type == "Season").ToList();
        var episodes = items.Where(x => x.Type == "Episode").ToList();
        var coveredSeries = new HashSet<Guid>();

        foreach (var show in series.OrderBy(x => x.Name))
        {
            var seasonCount = seasons.Count(x => x.SeriesId == show.Id);
            var episodeCount = episodes.Count(x => x.SeriesId == show.Id);
            var counts = Counts(seasonCount, episodeCount);
            var text = $"📺 <b>New show</b>\n<b>{E(show.Name)}{Year(show)}</b>";
            if (counts.Length > 0)
            {
                text += $"\n{counts}";
            }

            result.Add(Create(text, show, descriptions));
            coveredSeries.Add(show.Id);
        }

        seasons = seasons.Where(x => x.SeriesId is null || !coveredSeries.Contains(x.SeriesId.Value)).ToList();
        episodes = episodes.Where(x => x.SeriesId is null || !coveredSeries.Contains(x.SeriesId.Value)).ToList();
        var coveredSeasons = new HashSet<Guid>();

        foreach (var group in seasons.GroupBy(SeriesKey).OrderBy(x => x.First().SeriesName))
        {
            var values = group.OrderBy(x => x.SeasonNumber).ToList();
            var ids = values.Select(x => x.Id).ToHashSet();
            var childCount = episodes.Count(x => x.SeasonId is not null && ids.Contains(x.SeasonId.Value));
            var label = values.Count == 1 ? "season" : "seasons";
            var ranges = Ranges(values.Where(x => x.SeasonNumber.HasValue).Select(x => x.SeasonNumber!.Value), "Season ");
            var text = $"📚 <b>New {label}</b>\n<b>{E(SeriesName(values[0]))}</b>";
            if (ranges.Length > 0)
            {
                text += $" — {ranges}";
            }

            if (childCount > 0)
            {
                text += $"\n{childCount} episode{Plural(childCount)} included";
            }

            var description = descriptions ? values.FirstOrDefault(x => x.Overview.Length > 0)?.Overview : null;
            result.Add(new TelegramNotification(Description(text, description), values.FirstOrDefault(x => x.ImagePath is not null)?.ImagePath));
            coveredSeasons.UnionWith(ids);
        }

        episodes = episodes.Where(x => x.SeasonId is null || !coveredSeasons.Contains(x.SeasonId.Value)).ToList();
        foreach (var group in episodes.GroupBy(SeriesKey).OrderBy(x => x.First().SeriesName))
        {
            var values = group.OrderBy(x => x.SeasonNumber).ThenBy(x => x.EpisodeNumber).ToList();
            if (values.Count == 1)
            {
                var episode = values[0];
                var code = episode.SeasonNumber.HasValue && episode.EpisodeNumber.HasValue
                    ? $"S{episode.SeasonNumber:00}E{episode.EpisodeNumber:00} — "
                    : string.Empty;
                result.Add(Create($"🍿 <b>New episode</b>\n<b>{E(SeriesName(episode))}</b>\n{code}<i>{E(episode.Name)}</i>", episode, descriptions));
                continue;
            }

            var builder = new StringBuilder($"🍿 <b>New episodes</b>\n<b>{E(SeriesName(values[0]))}</b> · {values.Count} episodes");
            foreach (var season in values.GroupBy(x => x.SeasonNumber).Take(10))
            {
                var prefix = season.Key.HasValue ? $"S{season.Key:00}: " : string.Empty;
                var ranges = Ranges(season.Where(x => x.EpisodeNumber.HasValue).Select(x => x.EpisodeNumber!.Value), "E");
                var names = string.Join(", ", season.Take(3).Select(x => E(Shorten(x.Name, 70))));
                if (season.Count() > 3)
                {
                    names += $", +{season.Count() - 3} more";
                }

                builder.Append($"\n• {prefix}{ranges} — {names}");
            }

            var description = descriptions ? values.FirstOrDefault(x => x.Overview.Length > 0)?.Overview : null;
            result.Add(new TelegramNotification(Description(builder.ToString(), description), values.FirstOrDefault(x => x.ImagePath is not null)?.ImagePath));
        }

        return result;
    }

    private static TelegramNotification Create(string text, MediaItem item, bool descriptions) =>
        new(Description(text, descriptions ? item.Overview : null), item.ImagePath);

    private static string Description(string text, string? value) => string.IsNullOrWhiteSpace(value)
        ? text
        : $"{text}\n\n<b>Description</b>\n{E(Shorten(value, 900))}";

    private static string Counts(int seasons, int episodes)
    {
        var values = new List<string>();
        if (seasons > 0) values.Add($"{seasons} season{Plural(seasons)}");
        if (episodes > 0) values.Add($"{episodes} episode{Plural(episodes)}");
        return string.Join(" · ", values);
    }

    private static string Ranges(IEnumerable<int> source, string prefix)
    {
        var numbers = source.Distinct().Order().ToList();
        var result = new List<string>();
        for (var index = 0; index < numbers.Count;)
        {
            var start = numbers[index];
            var end = start;
            while (++index < numbers.Count && numbers[index] == end + 1) end = numbers[index];
            result.Add(start == end ? $"{prefix}{start:00}" : $"{prefix}{start:00}–{prefix}{end:00}");
        }

        return string.Join(", ", result);
    }

    private static string SeriesKey(MediaItem item) => item.SeriesId?.ToString() ?? item.SeriesName ?? item.Id.ToString();

    private static string SeriesName(MediaItem item) => item.SeriesName ?? item.Name;

    private static string Year(MediaItem item) => item.Year.HasValue ? $" ({item.Year})" : string.Empty;

    private static string Plural(int value) => value == 1 ? string.Empty : "s";

    private static string Shorten(string value, int length)
    {
        value = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return value.Length <= length ? value : value[..(length - 1)].TrimEnd() + "…";
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
