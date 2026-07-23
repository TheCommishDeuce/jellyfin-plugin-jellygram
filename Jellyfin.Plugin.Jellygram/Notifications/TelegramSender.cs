using System.Net.Http.Headers;
using Jellyfin.Plugin.Jellygram.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellygram.Notifications;

public sealed class TelegramSender(HttpClient httpClient, ILogger<TelegramSender> logger)
{
    public async Task SendAsync(TelegramNotification notification, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin is not initialized.");
        if (string.IsNullOrWhiteSpace(configuration.BotToken) || string.IsNullOrWhiteSpace(configuration.ChatId))
        {
            throw new InvalidOperationException("Jellygram bot token or chat ID is empty.");
        }

        byte[]? image = null;
        if (configuration.IncludePosters && notification.Text.Length <= 1024 && notification.ImagePath is not null)
        {
            image = await ReadImageAsync(notification.ImagePath, cancellationToken).ConfigureAwait(false);
        }

        if (configuration.IncludePosters && image is null)
        {
            logger.LogInformation(
                "Sending Jellygram notification without a poster because no readable primary image was available (image path present: {HasImagePath})",
                notification.ImagePath is not null);
        }

        using var request = image is not null
            ? PhotoRequest(configuration, notification, image)
            : MessageRequest(configuration, notification);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Telegram returned {(int)response.StatusCode}: {body[..Math.Min(body.Length, 500)]}");
        }
    }

    private async Task<byte[]?> ReadImageAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(path))
            {
                return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            }

            if (Uri.TryCreate(path, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return await httpClient.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException)
        {
            logger.LogWarning(exception, "Could not read the Jellyfin primary image; sending text only");
        }

        return null;
    }

    private static HttpRequestMessage MessageRequest(PluginConfiguration configuration, TelegramNotification notification)
    {
        var values = Common(configuration);
        values["text"] = notification.Text;
        values["link_preview_options"] = "{\"is_disabled\":true}";
        return Request(configuration.BotToken, "sendMessage", new FormUrlEncodedContent(values));
    }

    private static HttpRequestMessage PhotoRequest(PluginConfiguration configuration, TelegramNotification notification, byte[] imageBytes)
    {
        var content = new MultipartFormDataContent();
        foreach (var value in Common(configuration))
        {
            content.Add(new StringContent(value.Value), value.Key);
        }

        content.Add(new StringContent(notification.Text), "caption");
        var image = new ByteArrayContent(imageBytes);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(image, "photo", "poster.jpg");
        return Request(configuration.BotToken, "sendPhoto", content);
    }

    private static Dictionary<string, string> Common(PluginConfiguration configuration)
    {
        var values = new Dictionary<string, string>
        {
            ["chat_id"] = configuration.ChatId,
            ["parse_mode"] = "HTML",
            ["disable_notification"] = configuration.Silent ? "true" : "false"
        };
        if (!string.IsNullOrWhiteSpace(configuration.ThreadId))
        {
            values["message_thread_id"] = configuration.ThreadId;
        }

        return values;
    }

    private static HttpRequestMessage Request(string token, string method, HttpContent content) =>
        new(HttpMethod.Post, $"https://api.telegram.org/bot{token}/{method}") { Content = content };
}
