using SadBot.Modules.Birthdays;

namespace SadBot.Tests;

public sealed class CommandTests
{
    [Theory]
    [InlineData("123456789012345678", 123456789012345678UL)]
    [InlineData("<@123456789012345678>", 123456789012345678UL)]
    [InlineData("<@!123456789012345678>", 123456789012345678UL)]
    [InlineData("  123456789012345678 ", 123456789012345678UL)]
    public void ParsesUserIdsAndMentions(string input, ulong expected)
    {
        Assert.True(BirthdayCommandOptions.TryParseUserId(input, out var userId));
        Assert.Equal(expected, userId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("someone")]
    [InlineData("<@&123456789012345678>")]
    [InlineData("12")]
    public void RejectsInvalidUserIds(string? input) =>
        Assert.False(BirthdayCommandOptions.TryParseUserId(input, out _));

    [Fact]
    public void CommandDefinitionsPassCrovusValidation()
    {
        var user = new BirthdayCommands(null!, null!, null!, null!, TimeProvider.System).Command.Build();
        var admin = new BirthdayAdminCommands(null!, null!, null!, null!, null!, null!, TimeProvider.System).Command
            .Build();

        Assert.Equal("birthday", user.Name);
        Assert.Equal("birthday-admin", admin.Name);
    }

    [Fact]
    public void TimeZoneSearchFindsCities()
    {
        var catalog = new TimeZoneCatalog();

        Assert.Equal("Europe/Berlin", catalog.Search("berlin")[0]);
        Assert.Equal("America/New_York", catalog.Search("new york")[0]);
        Assert.NotEmpty(catalog.Search(null));
        Assert.All(catalog.Search("a"), id => Assert.Contains("a", id, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TimeZoneResolutionNormalizesInput()
    {
        var catalog = new TimeZoneCatalog();

        Assert.True(catalog.TryResolve("europe/berlin", out var id, out _));
        Assert.Equal("Europe/Berlin", id);
        Assert.False(catalog.TryResolve("Mars/Olympus_Mons", out _, out _));
    }
}
