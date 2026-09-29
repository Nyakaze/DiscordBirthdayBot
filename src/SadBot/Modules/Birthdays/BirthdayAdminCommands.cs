using System.Net;
using Crovus.Client;
using Crovus.Factory;
using Crovus.Models;
using Crovus.Rest;
using Microsoft.EntityFrameworkCore;
using SadBot.Data;
using SadBot.Discord;
using SadBot.Gifs;
using static SadBot.Modules.Birthdays.BirthdayCommandOptions;

namespace SadBot.Modules.Birthdays;

public sealed class BirthdayAdminCommands(
    CrovusClient client,
    BirthdayService birthdays,
    BirthdayBoard board,
    TimeZoneCatalog timeZones,
    IGifProvider gifs,
    IDbContextFactory<SadBotDbContext> contexts,
    TimeProvider time) : ICommandModule
{
    private const string Channel = "channel";
    private const string Role = "role";
    private const string Template = "template";

    private const DiscordPermissions RequiredPermissions = DiscordPermissions.ManageGuild;

    public SlashCommandFactory Command { get; } = SlashCommandFactory.Slash("birthday-admin",
            "Manage birthdays and birthday settings")
        .GuildOnly()
        .RequiringPermissions(RequiredPermissions)
        .AddSubCommand("set", "Add or change the birthday of a member",
            sub => sub.AddDateOptions().AddTargetOptions().AddYearOption())
        .AddSubCommand("remove", "Remove the birthday of a member", sub => sub.AddTargetOptions())
        .AddSubCommand("channel", "Set the channel for birthday announcements",
            sub => sub.AddOption(TextChannel("Where birthdays are announced")))
        .AddSubCommand("role", "Set the role members get on their birthday",
            sub => sub.AddOption(CommandOptionFactory.Role(Role, "The birthday role").AsRequired()))
        .AddSubCommand("message", "Set the announcement text ({user}, {name}, {age}). Leave empty to reset",
            sub => sub.AddOption(CommandOptionFactory.Text(Template, "The announcement text")
                .WithLength(1, CelebrationMessage.MaxTemplateLength)))
        .AddSubCommand("board", "Post the live birthday list in a channel",
            sub => sub.AddOption(TextChannel("Where the birthday list is posted")))
        .AddSubCommand("board-remove", "Delete the live birthday list")
        .AddSubCommand("settings", "Show the current birthday settings")
        .AddSubCommand("test", "Preview a birthday announcement (only visible to you)",
            sub => sub.AddOption(CommandOptionFactory.User(User, "The member to preview (defaults to you)")));

    public Task ExecuteAsync(DiscordInteraction interaction, CancellationToken cancellationToken)
    {
        if (interaction.GuildId is not { } guildId || interaction.Invoker is not { } invoker)
            return client.ReplyAsync(interaction, "This command only works on a server.", cancellationToken);

        if (!IsAdmin(interaction))
            return client.ReplyAsync(interaction, "You need the **Manage Server** permission for this.",
                cancellationToken);

        return interaction.SubCommand switch
        {
            "set" => SetAsync(interaction, guildId, invoker.Id, cancellationToken),
            "remove" => RemoveAsync(interaction, guildId, cancellationToken),
            "channel" => SetChannelAsync(interaction, guildId, cancellationToken),
            "role" => SetRoleAsync(interaction, guildId, cancellationToken),
            "message" => SetTemplateAsync(interaction, guildId, cancellationToken),
            "board" => PublishBoardAsync(interaction, guildId, cancellationToken),
            "board-remove" => RemoveBoardAsync(interaction, guildId, cancellationToken),
            "settings" => ShowSettingsAsync(interaction, guildId, cancellationToken),
            "test" => PreviewAsync(interaction, guildId, invoker, cancellationToken),
            _ => client.ReplyAsync(interaction, "Unknown command.", cancellationToken)
        };
    }

    public Task AutocompleteAsync(DiscordInteraction interaction, CancellationToken cancellationToken) =>
        client.SuggestTimeZonesAsync(interaction, timeZones, cancellationToken);

    public static bool IsAdmin(DiscordInteraction interaction) =>
        interaction.Member?.Permissions is { } permissions &&
        (permissions.HasFlag(DiscordPermissions.Administrator) || permissions.HasFlag(RequiredPermissions));

    private static CommandOptionFactory TextChannel(string description) =>
        CommandOptionFactory.Channel(Channel, description).AsRequired()
            .WithChannelTypes(ChannelType.GuildText, ChannelType.GuildAnnouncement);

    private async Task SetAsync(DiscordInteraction interaction, ulong guildId, ulong invokerId,
        CancellationToken cancellationToken)
    {
        if (!TryGetTarget(interaction, out var userId))
        {
            await client.ReplyAsync(interaction, "Pick a member with `user` or paste their id into `user_id`.",
                cancellationToken);
            return;
        }

        var result = await birthdays.SaveAsync(guildId, userId,
            (int)(interaction.GetInteger(Month) ?? 0), (int)(interaction.GetInteger(Day) ?? 0),
            (int?)interaction.GetInteger(Year), interaction.GetString(TimeZoneOption) ?? string.Empty, invokerId,
            cancellationToken);

        if (result.Birthday is not { } birthday)
        {
            await client.ReplyAsync(interaction, DescribeFailure(result.Status), cancellationToken);
            return;
        }

        await client.ReplyAsync(interaction,
            $"🎂 {(result.Status is SaveBirthdayStatus.Created ? "Added" : "Updated")} the birthday of <@{userId}>: " +
            $"**{BirthdayCalendar.Format(birthday.Month, birthday.Day, birthday.Year)}** ({birthday.TimeZoneId}).",
            cancellationToken);
    }

    private async Task RemoveAsync(DiscordInteraction interaction, ulong guildId, CancellationToken cancellationToken)
    {
        if (!TryGetTarget(interaction, out var userId))
        {
            await client.ReplyAsync(interaction, "Pick a member with `user` or paste their id into `user_id`.",
                cancellationToken);
            return;
        }

        var removed = await birthdays.RemoveAsync(guildId, userId, cancellationToken);

        await client.ReplyAsync(interaction,
            removed ? $"Removed the birthday of <@{userId}>." : $"<@{userId}> has no saved birthday.",
            cancellationToken);
    }

    private async Task SetChannelAsync(DiscordInteraction interaction, ulong guildId,
        CancellationToken cancellationToken)
    {
        var channelId = interaction.GetSnowflake(Channel)!.Value.Value;

        await birthdays.UpdateSettingsAsync(guildId, settings => settings.AnnouncementChannelId = channelId,
            cancellationToken);

        await client.ReplyAsync(interaction, $"Birthdays will be announced in <#{channelId}>.", cancellationToken);
    }

    private async Task SetRoleAsync(DiscordInteraction interaction, ulong guildId, CancellationToken cancellationToken)
    {
        var roleId = interaction.GetSnowflake(Role)!.Value.Value;

        await birthdays.UpdateSettingsAsync(guildId, settings => settings.BirthdayRoleId = roleId, cancellationToken);

        await client.ReplyAsync(interaction,
            $"Birthday members will get <@&{roleId}>. Make sure my role is above it so I can assign it.",
            cancellationToken);
    }

    private async Task SetTemplateAsync(DiscordInteraction interaction, ulong guildId,
        CancellationToken cancellationToken)
    {
        var template = interaction.GetString(Template)?.Trim();

        await birthdays.UpdateSettingsAsync(guildId,
            settings => settings.MessageTemplate = string.IsNullOrEmpty(template) ? null : template,
            cancellationToken);

        var reply = string.IsNullOrEmpty(template)
            ? $"The announcement text was reset to: {BirthdayService.DefaultTemplate}"
            : "The announcement text was updated. Use `/birthday-admin test` to preview it.";

        if (template is { Length: > 0 } && !template.Contains("{user}", StringComparison.OrdinalIgnoreCase))
            reply += "\n⚠️ The text has no `{user}` placeholder, so the birthday person won't be pinged.";

        await client.ReplyAsync(interaction, reply, cancellationToken);
    }

    private async Task PublishBoardAsync(DiscordInteraction interaction, ulong guildId,
        CancellationToken cancellationToken)
    {
        var channelId = interaction.GetSnowflake(Channel)!.Value.Value;

        await client.Interactions.DeferAsync(interaction, ephemeral: true, cancellationToken);

        try
        {
            await board.PublishAsync(guildId, channelId, cancellationToken);
            await client.EditReplyAsync(interaction,
                $"The birthday list is live in <#{channelId}> and updates automatically.", cancellationToken);
        }
        catch (DiscordRestException exception) when (exception.StatusCode is HttpStatusCode.Forbidden
                                                          or HttpStatusCode.NotFound)
        {
            await client.EditReplyAsync(interaction,
                $"I can't post in <#{channelId}>. I need **View Channel**, **Send Messages** and **Embed Links** there.",
                cancellationToken);
        }
    }

    private async Task RemoveBoardAsync(DiscordInteraction interaction, ulong guildId,
        CancellationToken cancellationToken)
    {
        await client.Interactions.DeferAsync(interaction, ephemeral: true, cancellationToken);
        await board.RemoveAsync(guildId, cancellationToken);
        await client.EditReplyAsync(interaction, "The birthday list was removed.", cancellationToken);
    }

    private async Task ShowSettingsAsync(DiscordInteraction interaction, ulong guildId,
        CancellationToken cancellationToken)
    {
        var settings = await birthdays.GetSettingsAsync(guildId, cancellationToken);

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var boardChannel = await db.BoardMessages.Where(message => message.GuildId == guildId)
            .Select(message => (ulong?)message.ChannelId)
            .FirstOrDefaultAsync(cancellationToken);

        var count = await db.Birthdays.CountAsync(birthday => birthday.GuildId == guildId, cancellationToken);

        var embed = EmbedFactory.Create()
            .WithTitle("🎂 Birthday settings")
            .WithColor(BirthdayBoardRenderer.Color)
            .AddField("Announcement channel",
                settings.AnnouncementChannelId is { } channel ? $"<#{channel}>" : "⚠️ Not set, nobody gets celebrated",
                true)
            .AddField("Birthday role",
                settings.BirthdayRoleId is { } role ? $"<@&{role}>" : $"Created automatically as \"{BirthdayScheduler.RoleName}\"",
                true)
            .AddField("Birthday list", boardChannel is { } list ? $"<#{list}>" : "Not posted", true)
            .AddField("Saved birthdays", count.ToString(), true)
            .AddField("Announcement text", settings.MessageTemplate ?? BirthdayService.DefaultTemplate)
            .Build();

        await client.ReplyAsync(interaction, [embed], cancellationToken);
    }

    private async Task PreviewAsync(DiscordInteraction interaction, ulong guildId, DiscordUser invoker,
        CancellationToken cancellationToken)
    {
        await client.Interactions.DeferAsync(interaction, ephemeral: true, cancellationToken);

        var userId = interaction.GetSnowflake(User)?.Value ?? invoker.Id.Value;
        var member = await client.Members.GetAsync(guildId, userId, cancellationToken);
        var birthday = await birthdays.FindAsync(guildId, userId, cancellationToken);
        var settings = await birthdays.GetSettingsAsync(guildId, cancellationToken);
        var gif = await gifs.GetRandomAsync(cancellationToken);

        var age = birthday?.Year is { } year ? time.GetUtcNow().Year - year : (int?)null;
        var preview = CelebrationMessage.Build(settings.MessageTemplate, userId, member.DisplayName, age, gif);

        await client.Interactions.EditResponseAsync(interaction,
            new InteractionMessageRequest(preview.Content, preview.Embeds,
                AllowedMentions: DiscordAllowedMentions.None), cancellationToken);
    }
}
