using System.Text;
using Crovus.Client;
using Crovus.Factory;
using Crovus.Models;
using SadBot.Discord;
using static SadBot.Modules.Birthdays.BirthdayCommandOptions;

namespace SadBot.Modules.Birthdays;

public sealed class BirthdayCommands(
    CrovusClient client,
    BirthdayService birthdays,
    BirthdayBoard board,
    TimeZoneCatalog timeZones,
    TimeProvider time) : ICommandModule
{
    public const int UpcomingCount = 10;

    public SlashCommandFactory Command { get; } = SlashCommandFactory.Slash("birthday", "Birthday commands")
        .GuildOnly()
        .AddSubCommand("set", "Add or change your birthday", sub => sub.AddDateOptions().AddYearOption())
        .AddSubCommand("remove", "Remove your birthday")
        .AddSubCommand("show", "Show the birthday of a member",
            sub => sub.AddOption(CommandOptionFactory.User(User, "The member (defaults to you)")))
        .AddSubCommand("list", "Show every birthday on this server")
        .AddSubCommand("next", "Show the upcoming birthdays");

    public Task ExecuteAsync(DiscordInteraction interaction, CancellationToken cancellationToken)
    {
        if (interaction.GuildId is not { } guildId || interaction.Invoker is not { } invoker)
            return client.ReplyAsync(interaction, "This command only works on a server.", cancellationToken);

        return interaction.SubCommand switch
        {
            "set" => SetAsync(interaction, guildId, invoker.Id, cancellationToken),
            "remove" => RemoveAsync(interaction, guildId, invoker.Id, cancellationToken),
            "show" => ShowAsync(interaction, guildId, interaction.GetSnowflake(User) ?? invoker.Id, invoker.Id,
                cancellationToken),
            "list" => ListAsync(interaction, guildId, cancellationToken),
            "next" => NextAsync(interaction, guildId, cancellationToken),
            _ => client.ReplyAsync(interaction, "Unknown command.", cancellationToken)
        };
    }

    public Task AutocompleteAsync(DiscordInteraction interaction, CancellationToken cancellationToken) =>
        client.SuggestTimeZonesAsync(interaction, timeZones, cancellationToken);

    private async Task SetAsync(DiscordInteraction interaction, ulong guildId, ulong userId,
        CancellationToken cancellationToken)
    {
        var result = await birthdays.SaveAsync(guildId, userId,
            (int)(interaction.GetInteger(Month) ?? 0), (int)(interaction.GetInteger(Day) ?? 0),
            (int?)interaction.GetInteger(Year), interaction.GetString(TimeZoneOption) ?? string.Empty, userId,
            cancellationToken);

        if (result.Birthday is not { } birthday)
        {
            await client.ReplyAsync(interaction, DescribeFailure(result.Status), cancellationToken);
            return;
        }

        await client.ReplyAsync(interaction,
            $"🎂 Saved your birthday: **{BirthdayCalendar.Format(birthday.Month, birthday.Day, birthday.Year)}** " +
            $"({birthday.TimeZoneId}).", cancellationToken);
    }

    private async Task RemoveAsync(DiscordInteraction interaction, ulong guildId, ulong userId,
        CancellationToken cancellationToken)
    {
        var removed = await birthdays.RemoveAsync(guildId, userId, cancellationToken);

        await client.ReplyAsync(interaction,
            removed ? "Your birthday was removed." : "You haven't saved a birthday on this server.",
            cancellationToken);
    }

    private async Task ShowAsync(DiscordInteraction interaction, ulong guildId, ulong userId, ulong invokerId,
        CancellationToken cancellationToken)
    {
        var birthday = await birthdays.FindAsync(guildId, userId, cancellationToken);
        var self = userId == invokerId;

        if (birthday is null)
        {
            await client.ReplyAsync(interaction,
                self
                    ? "You haven't saved a birthday yet. Use `/birthday set`."
                    : $"<@{userId}> hasn't saved a birthday yet.", cancellationToken);
            return;
        }

        var next = birthdays.NextOccurrence(birthday, time.GetUtcNow());
        var date = BirthdayCalendar.Format(birthday.Month, birthday.Day, self ? birthday.Year : null);

        await client.ReplyAsync(interaction,
            $"🎂 {(self ? "Your" : $"<@{userId}>'s")} birthday is **{date}** ({birthday.TimeZoneId}), " +
            $"next one {Relative(next, birthday)}.", cancellationToken);
    }

    private async Task ListAsync(DiscordInteraction interaction, ulong guildId, CancellationToken cancellationToken)
    {
        var pages = await board.RenderAsync(guildId, cancellationToken);

        await client.ReplyAsync(interaction, [pages[0]], cancellationToken);

        foreach (var page in pages.Skip(1))
            await client.FollowUpAsync(interaction, [page], cancellationToken);
    }

    private async Task NextAsync(DiscordInteraction interaction, ulong guildId, CancellationToken cancellationToken)
    {
        var upcoming = await birthdays.UpcomingAsync(guildId, UpcomingCount, cancellationToken);

        if (upcoming.Count == 0)
        {
            await client.ReplyAsync(interaction, "No birthdays saved yet. Use `/birthday set`.", cancellationToken);
            return;
        }

        var now = time.GetUtcNow();
        var lines = new StringBuilder();

        foreach (var birthday in upcoming)
            lines.AppendLine(
                $"**{BirthdayCalendar.Format(birthday.Month, birthday.Day, null)}** · <@{birthday.UserId}> · " +
                Relative(birthdays.NextOccurrence(birthday, now), birthday));

        await client.ReplyAsync(interaction,
        [
            EmbedFactory.Create()
                .WithTitle("🎈 Upcoming birthdays")
                .WithDescription(lines.ToString())
                .WithColor(BirthdayBoardRenderer.Color)
                .Build()
        ], cancellationToken);
    }

    private string Relative(DateOnly next, Data.Birthday birthday)
    {
        var zone = timeZones.TryResolve(birthday.TimeZoneId, out _, out var found) ? found : TimeZoneInfo.Utc;
        var days = next.DayNumber - BirthdayCalendar.LocalToday(time.GetUtcNow(), zone).DayNumber;

        return days switch
        {
            0 => "**today** 🎉",
            1 => "tomorrow",
            _ => $"in {days} days"
        };
    }
}
