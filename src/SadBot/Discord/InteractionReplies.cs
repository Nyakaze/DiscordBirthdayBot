using Crovus.Client;
using Crovus.Models;

namespace SadBot.Discord;

public static class InteractionReplies
{
    public static Task ReplyAsync(this CrovusClient client, DiscordInteraction interaction, string content,
        CancellationToken cancellationToken) =>
        client.Interactions.RespondAsync(interaction,
            response => response.WithContent(content).AsEphemeral().SuppressMentions(), cancellationToken);

    public static Task ReplyAsync(this CrovusClient client, DiscordInteraction interaction,
        IReadOnlyList<DiscordEmbed> embeds, CancellationToken cancellationToken, string? content = null) =>
        client.Interactions.RespondAsync(interaction, response =>
        {
            response.WithContent(content).AsEphemeral().SuppressMentions();

            foreach (var embed in embeds)
                response.AddEmbed(embed);
        }, cancellationToken);

    public static Task<DiscordMessage> FollowUpAsync(this CrovusClient client, DiscordInteraction interaction,
        IReadOnlyList<DiscordEmbed> embeds, CancellationToken cancellationToken) =>
        client.Interactions.FollowUpAsync(interaction, new InteractionMessageRequest(Embeds: embeds, Ephemeral: true,
            AllowedMentions: DiscordAllowedMentions.None), cancellationToken);

    public static Task<DiscordMessage> EditReplyAsync(this CrovusClient client, DiscordInteraction interaction,
        string content, CancellationToken cancellationToken) =>
        client.Interactions.EditResponseAsync(interaction,
            new InteractionMessageRequest(content, AllowedMentions: DiscordAllowedMentions.None), cancellationToken);
}
