using System.Net;
using Crovus.Client;
using Crovus.Factory;
using Crovus.Models;
using Crovus.Rest;
using SadBot.Discord;

namespace SadBot.Modules.Moderation;

public sealed class RenameCommands(CrovusClient client) : ICommandModule
{
    private const string User = "user";
    private const string Name = "name";

    private const DiscordPermissions RequiredPermissions = DiscordPermissions.ManageNicknames;

    public SlashCommandFactory Command { get; } = SlashCommandFactory.Slash("rename", "Change the nickname of a member")
        .GuildOnly()
        .RequiringPermissions(RequiredPermissions)
        .AddOption(CommandOptionFactory.User(User, "The member to rename").AsRequired())
        .AddOption(CommandOptionFactory.Text(Name, "The new nickname")
            .WithLength(1, DiscordLimits.Nickname)
            .AsRequired());

    public async Task ExecuteAsync(DiscordInteraction interaction, CancellationToken cancellationToken)
    {
        if (interaction.GuildId is not { } guildId || interaction.Invoker is not { } invoker)
        {
            await client.ReplyAsync(interaction, "This command only works on a server.", cancellationToken);
            return;
        }

        if (!CanRename(interaction))
        {
            await client.ReplyAsync(interaction, "You need the **Manage Nicknames** permission for this.",
                cancellationToken);
            return;
        }

        var userId = interaction.GetSnowflake(User)!.Value;
        var name = interaction.GetString(Name)?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            await client.ReplyAsync(interaction, "The new nickname can't be empty.", cancellationToken);
            return;
        }

        try
        {
            await client.Members.SetNicknameAsync(guildId, userId, name, $"Renamed by {invoker.Username}",
                cancellationToken);
        }
        catch (DiscordRestException exception) when (exception.StatusCode is HttpStatusCode.Forbidden)
        {
            await client.ReplyAsync(interaction,
                $"I can't rename <@{userId}>. I need **Manage Nicknames** and my role must be above theirs " +
                "(the server owner can't be renamed).", cancellationToken);
            return;
        }
        catch (DiscordRestException exception) when (exception.StatusCode is HttpStatusCode.NotFound)
        {
            await client.ReplyAsync(interaction, $"<@{userId}> isn't a member of this server.", cancellationToken);
            return;
        }

        await client.ReplyAsync(interaction, $"Renamed <@{userId}> to **{name}**.", cancellationToken);
    }

    private static bool CanRename(DiscordInteraction interaction) =>
        interaction.Member?.Permissions is { } permissions &&
        (permissions.HasFlag(DiscordPermissions.Administrator) || permissions.HasFlag(RequiredPermissions));
}
