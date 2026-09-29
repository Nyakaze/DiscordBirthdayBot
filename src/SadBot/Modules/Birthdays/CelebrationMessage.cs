using Crovus.Factory;
using Crovus.Models;
using SadBot.Gifs;

namespace SadBot.Modules.Birthdays;

public static class CelebrationMessage
{
    public const int MaxTemplateLength = 1500;

    public static string Content(string? template, ulong userId, string displayName, int? age) =>
        (string.IsNullOrWhiteSpace(template) ? BirthdayService.DefaultTemplate : template)
        .Replace("{user}", $"<@{userId}>", StringComparison.OrdinalIgnoreCase)
        .Replace("{name}", displayName, StringComparison.OrdinalIgnoreCase)
        .Replace("{age}", age?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
        .Trim();

    public static DiscordEmbed Embed(string displayName, int? age, Gif? gif)
    {
        var embed = EmbedFactory.Create()
            .WithTitle($"Happy Birthday, {displayName}! 🥳")
            .WithColor(BirthdayBoardRenderer.Color)
            .WithDescription(age is { } years
                ? $"Turning **{years}** today! Everyone, go wish them a great day! 🎈"
                : "Everyone, go wish them a great day! 🎈");

        if (gif is not null)
            embed.WithImage(gif.Url);

        if (gif?.Attribution is { } attribution)
            embed.WithFooter(attribution);

        return embed.Build();
    }

    public static MessageCreateRequest Build(string? template, ulong userId, string displayName, int? age,
        Gif? gif) =>
        new(Content(template, userId, displayName, age), [Embed(displayName, age, gif)],
            AllowedMentions: DiscordAllowedMentions.OnlyUsers(userId));
}
