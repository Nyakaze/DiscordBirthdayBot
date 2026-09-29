using SadBot.Modules.Birthdays;

namespace SadBot.Tests;

public sealed class BirthdayCalendarTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly TimeZoneInfo LosAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    [Fact]
    public void LeapDayBirthdaysAreCelebratedOnFebruary28InCommonYears()
    {
        Assert.True(BirthdayCalendar.IsBirthday(new DateOnly(2027, 2, 28), 2, 29));
        Assert.False(BirthdayCalendar.IsBirthday(new DateOnly(2027, 3, 1), 2, 29));
        Assert.True(BirthdayCalendar.IsBirthday(new DateOnly(2028, 2, 29), 2, 29));
        Assert.False(BirthdayCalendar.IsBirthday(new DateOnly(2028, 2, 28), 2, 29));
    }

    [Fact]
    public void LocalTodayFollowsTheMembersTimeZone()
    {
        var instant = new DateTimeOffset(2026, 3, 14, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 3, 15), BirthdayCalendar.LocalToday(instant, Berlin));
        Assert.Equal(new DateOnly(2026, 3, 14), BirthdayCalendar.LocalToday(instant, LosAngeles));
    }

    [Fact]
    public void NextOccurrenceRollsOverToNextYear()
    {
        Assert.Equal(new DateOnly(2026, 12, 24), BirthdayCalendar.NextOccurrence(new DateOnly(2026, 9, 29), 12, 24));
        Assert.Equal(new DateOnly(2027, 1, 5), BirthdayCalendar.NextOccurrence(new DateOnly(2026, 9, 29), 1, 5));
        Assert.Equal(new DateOnly(2026, 9, 29), BirthdayCalendar.NextOccurrence(new DateOnly(2026, 9, 29), 9, 29));
    }

    [Theory]
    [InlineData(2, 29, null, true)]
    [InlineData(2, 30, null, false)]
    [InlineData(4, 31, null, false)]
    [InlineData(2, 29, 2000, true)]
    [InlineData(2, 29, 2001, false)]
    [InlineData(5, 1, 1899, false)]
    [InlineData(12, 1, 2026, false)]
    [InlineData(9, 29, 2026, true)]
    [InlineData(13, 1, null, false)]
    public void ValidatesDates(int month, int day, int? year, bool expected) =>
        Assert.Equal(expected, BirthdayCalendar.IsValid(month, day, year, new DateOnly(2026, 9, 29)));

    [Fact]
    public void CandidateMonthsCoverEveryTimeZoneAroundAMonthBoundary()
    {
        var months = BirthdayCalendar.CandidateMonths(new DateTimeOffset(2026, 9, 30, 20, 0, 0, TimeSpan.Zero));

        Assert.Equal(new HashSet<int> { 9, 10 }, months);
    }

    [Fact]
    public void AgeIsOnlyKnownWithABirthYear()
    {
        Assert.Equal(30, BirthdayCalendar.AgeOn(new DateOnly(2026, 5, 1), 1996));
        Assert.Null(BirthdayCalendar.AgeOn(new DateOnly(2026, 5, 1), null));
    }
}
