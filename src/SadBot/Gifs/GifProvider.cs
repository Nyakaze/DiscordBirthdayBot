using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SadBot.Gifs;

public sealed class GifOptions
{
    public const string Section = "Gifs";

    public string? GiphyApiKey { get; set; }

    public string Tag { get; set; } = "happy birthday";

    public string Rating { get; set; } = "g";

    public List<string> Fallback { get; set; } = [];
}

public sealed record Gif(string Url, string? Attribution);

public interface IGifProvider
{
    Task<Gif?> GetRandomAsync(CancellationToken cancellationToken);
}

public sealed class GiphyGifProvider(HttpClient http, IOptionsMonitor<GifOptions> options,
    ILogger<GiphyGifProvider> logger) : IGifProvider
{
    public async Task<Gif?> GetRandomAsync(CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;

        if (!string.IsNullOrWhiteSpace(settings.GiphyApiKey))
        {
            try
            {
                if (await FetchFromGiphyAsync(settings, cancellationToken) is { } gif)
                    return gif;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                              exception is HttpRequestException or JsonException or TaskCanceledException)
            {
                logger.LogWarning(exception, "Could not fetch a GIF from Giphy, using the fallback list");
            }
        }

        return settings.Fallback.Count == 0
            ? null
            : new Gif(settings.Fallback[Random.Shared.Next(settings.Fallback.Count)], null);
    }

    private async Task<Gif?> FetchFromGiphyAsync(GifOptions settings, CancellationToken cancellationToken)
    {
        var url = "https://api.giphy.com/v1/gifs/random" +
                  $"?api_key={Uri.EscapeDataString(settings.GiphyApiKey!)}" +
                  $"&tag={Uri.EscapeDataString(settings.Tag)}" +
                  $"&rating={Uri.EscapeDataString(settings.Rating)}";

        using var response = await http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return ParseRandomResponse(document.RootElement);
    }

    public static Gif? ParseRandomResponse(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind is not JsonValueKind.Object)
            return null;

        if (!data.TryGetProperty("images", out var images) ||
            !images.TryGetProperty("original", out var original) ||
            !original.TryGetProperty("url", out var url) ||
            url.GetString() is not { Length: > 0 } gifUrl)
            return null;

        return new Gif(gifUrl, "Powered by GIPHY");
    }
}
