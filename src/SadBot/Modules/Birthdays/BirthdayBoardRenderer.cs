using System.Text;
using Crovus.Factory;
using Crovus.Models;

namespace SadBot.Modules.Birthdays;

public sealed record BoardEntry(ulong UserId, int Month, int Day, bool Celebrating);

public static class BirthdayBoardRenderer
{
    public const int Color = 0xFF69B4;
    public const int PageCharacterBudget = 5500;
    public const int FieldsPerPage = DiscordLimits.EmbedFields;

    private const string Title = "🎂 Birthday List";
    private const string Hint = "Add yours with `/birthday set` and it shows up here automatically.";

    public static IReadOnlyList<DiscordEmbed> Render(IEnumerable<BoardEntry> entries, DateTimeOffset updatedAt)
    {
        var sorted = entries
            .OrderBy(entry => entry.Month)
            .ThenBy(entry => entry.Day)
            .ThenBy(entry => entry.UserId)
            .ToArray();

        var footer = sorted.Length == 1 ? "1 birthday" : $"{sorted.Length} birthdays";

        if (sorted.Length == 0)
            return
            [
                EmbedFactory.Create()
                    .WithTitle(Title)
                    .WithDescription($"No birthdays yet.\n{Hint}")
                    .WithColor(Color)
                    .WithFooter(footer)
                    .WithTimestamp(updatedAt)
                    .Build()
            ];

        var pages = Paginate(BuildFields(sorted));

        return pages
            .Select((fields, index) =>
            {
                var embed = EmbedFactory.Create()
                    .WithColor(Color)
                    .AddFields(fields);

                if (index == 0)
                    embed.WithTitle(Title).WithDescription(Hint);

                if (index == pages.Count - 1)
                    embed.WithFooter(pages.Count == 1 ? footer : $"{footer} · page {index + 1}/{pages.Count}")
                        .WithTimestamp(updatedAt);
                else
                    embed.WithFooter($"page {index + 1}/{pages.Count}");

                return embed.Build();
            })
            .ToArray();
    }

    public static string FormatLine(BoardEntry entry) =>
        $"`{entry.Day,2}.` <@{entry.UserId}>{(entry.Celebrating ? " 🎉" : string.Empty)}";

    private static List<DiscordEmbedField> BuildFields(IEnumerable<BoardEntry> sorted)
    {
        var fields = new List<DiscordEmbedField>();

        foreach (var month in sorted.GroupBy(entry => entry.Month))
        {
            var name = BirthdayCalendar.MonthName(month.Key);
            var value = new StringBuilder();

            foreach (var line in month.Select(FormatLine))
            {
                if (value.Length + line.Length + 1 > DiscordLimits.EmbedFieldValue)
                {
                    fields.Add(new DiscordEmbedField(name, value.ToString(), false));
                    name = $"{BirthdayCalendar.MonthName(month.Key)} (cont.)";
                    value.Clear();
                }

                if (value.Length > 0)
                    value.Append('\n');

                value.Append(line);
            }

            fields.Add(new DiscordEmbedField(name, value.ToString(), false));
        }

        return fields;
    }

    private static List<List<DiscordEmbedField>> Paginate(IEnumerable<DiscordEmbedField> fields)
    {
        var pages = new List<List<DiscordEmbedField>> { new() };
        var used = Title.Length + Hint.Length;

        foreach (var field in fields)
        {
            var size = field.Name.Length + field.Value.Length;
            var page = pages[^1];

            if (page.Count > 0 && (page.Count == FieldsPerPage || used + size > PageCharacterBudget))
            {
                pages.Add(page = new List<DiscordEmbedField>());
                used = 0;
            }

            page.Add(field);
            used += size;
        }

        return pages;
    }
}
