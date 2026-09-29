using Crovus.Factory;
using SadBot.Modules.Birthdays;

namespace SadBot.Tests;

public sealed class BirthdayBoardRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SortsByMonthThenDay()
    {
        var pages = BirthdayBoardRenderer.Render(
        [
            new BoardEntry(3, 12, 1, false),
            new BoardEntry(1, 3, 20, false),
            new BoardEntry(2, 3, 5, true)
        ], Now);

        var page = Assert.Single(pages);

        Assert.Equal(["March", "December"], page.Fields.Select(field => field.Name));
        Assert.Equal("` 5.` <@2> 🎉\n`20.` <@1>", page.Fields[0].Value);
        Assert.Equal("` 1.` <@3>", page.Fields[1].Value);
        Assert.Equal("3 birthdays", page.Footer?.Text);
    }

    [Fact]
    public void RendersAnEmptyBoard()
    {
        var page = Assert.Single(BirthdayBoardRenderer.Render([], Now));

        Assert.Contains("No birthdays yet", page.Description);
        Assert.Empty(page.Fields);
    }

    [Fact]
    public void LargeBoardsStayWithinDiscordLimits()
    {
        var entries = Enumerable.Range(0, 1500)
            .Select(index => new BoardEntry(100_000_000_000_000_000UL + (ulong)index, index % 12 + 1, index % 28 + 1,
                index % 50 == 0));

        var pages = BirthdayBoardRenderer.Render(entries, Now);

        Assert.True(pages.Count > 1);

        foreach (var page in pages)
        {
            Assert.True(page.Fields.Count <= DiscordLimits.EmbedFields);
            Assert.All(page.Fields, field => Assert.True(field.Value.Length <= DiscordLimits.EmbedFieldValue));

            var total = (page.Title?.Length ?? 0) + (page.Description?.Length ?? 0) + (page.Footer?.Text.Length ?? 0) +
                        page.Fields.Sum(field => field.Name.Length + field.Value.Length);

            Assert.True(total <= 6000, $"Page has {total} characters");
        }

        var lines = pages.SelectMany(page => page.Fields).Sum(field => field.Value.Split('\n').Length);
        Assert.Equal(1500, lines);
    }
}
