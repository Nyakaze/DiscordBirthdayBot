using System.Text.RegularExpressions;
using Crovus.Factory;
using Crovus.Models;

namespace SadBot.Modules.Birthdays;

public static partial class BirthdayCommandOptions
{
    public const string Day = "day";
    public const string Month = "month";
    public const string Year = "year";
    public const string TimeZoneOption = "timezone";
    public const string User = "user";
    public const string UserId = "user_id";

    public static CommandOptionFactory AddDateOptions(this CommandOptionFactory command)
    {
        var month = CommandOptionFactory.Integer(Month, "Month of the birthday").AsRequired();

        for (var index = 1; index <= 12; index++)
            month.WithChoice(BirthdayCalendar.MonthName(index), index);

        return command
            .AddOption(CommandOptionFactory.Integer(Day, "Day of the birthday").AsRequired().WithRange(1, 31))
            .AddOption(month)
            .AddOption(CommandOptionFactory.Text(TimeZoneOption, "Your time zone, e.g. Europe/Berlin").AsRequired()
                .WithAutocomplete());
    }

    public static CommandOptionFactory AddYearOption(this CommandOptionFactory command) =>
        command.AddOption(CommandOptionFactory.Integer(Year, "Birth year (optional, used to show the age)")
            .WithRange(BirthdayCalendar.MinimumYear, 2100));

    public static CommandOptionFactory AddTargetOptions(this CommandOptionFactory command) =>
        command
            .AddOption(CommandOptionFactory.User(User, "The member (@mention)"))
            .AddOption(CommandOptionFactory.Text(UserId, "The user id, if the member can't be mentioned")
                .WithLength(1, 32));

    public static bool TryGetTarget(DiscordInteraction interaction, out ulong userId)
    {
        userId = 0;

        if (interaction.GetSnowflake(User) is { } user)
        {
            userId = user.Value;
            return true;
        }

        return TryParseUserId(interaction.GetString(UserId), out userId);
    }

    public static bool TryParseUserId(string? input, out ulong userId)
    {
        userId = 0;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var match = UserIdPattern().Match(input.Trim());

        return match.Success && ulong.TryParse(match.Groups["id"].Value, out userId) && userId > 0;
    }

    public static async Task SuggestTimeZonesAsync(this Crovus.Client.CrovusClient client,
        DiscordInteraction interaction, TimeZoneCatalog timeZones, CancellationToken cancellationToken)
    {
        if (interaction.FocusedOption is not { Name: TimeZoneOption } focused)
            return;

        await client.Interactions.AutocompleteAsync(interaction, timeZones.Search(focused.AsString),
            cancellationToken);
    }

    public static string DescribeFailure(SaveBirthdayStatus status) => status switch
    {
        SaveBirthdayStatus.InvalidTimeZone =>
            "I don't know that time zone. Pick one from the suggestions, e.g. `Europe/Berlin`.",
        SaveBirthdayStatus.InvalidDate => "That date doesn't exist or lies in the future.",
        _ => "The birthday could not be saved."
    };

    [GeneratedRegex(@"^(?:<@!?)?(?<id>\d{15,21})>?$")]
    private static partial Regex UserIdPattern();
}
