using System.Globalization;

namespace SadBot.Modules.Birthdays;

public static class BirthdayCalendar
{
    public const int MinimumYear = 1900;

    public static string MonthName(int month) =>
        CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);

    public static bool IsValid(int month, int day, int? year, DateOnly today)
    {
        if (month is < 1 or > 12 || day < 1)
            return false;

        if (year is { } birthYear)
            return birthYear >= MinimumYear && day <= DateTime.DaysInMonth(birthYear, month) &&
                   new DateOnly(birthYear, month, day) <= today;

        return day <= DateTime.DaysInMonth(2024, month);
    }

    public static DateOnly OccurrenceIn(int year, int month, int day) =>
        month == 2 && day == 29 && !DateTime.IsLeapYear(year)
            ? new DateOnly(year, 2, 28)
            : new DateOnly(year, month, day);

    public static DateOnly LocalToday(DateTimeOffset now, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

    public static bool IsBirthday(DateOnly localDate, int month, int day) =>
        OccurrenceIn(localDate.Year, month, day) == localDate;

    public static DateOnly NextOccurrence(DateOnly from, int month, int day)
    {
        var occurrence = OccurrenceIn(from.Year, month, day);

        return occurrence >= from ? occurrence : OccurrenceIn(from.Year + 1, month, day);
    }

    public static int? AgeOn(DateOnly date, int? birthYear) => birthYear is { } year ? date.Year - year : null;

    public static IReadOnlySet<int> CandidateMonths(DateTimeOffset now)
    {
        var utc = now.UtcDateTime;

        return new HashSet<int> { utc.AddDays(-1).Month, utc.Month, utc.AddDays(1).Month };
    }

    public static string Format(int month, int day, int? year) =>
        year is { } birthYear
            ? $"{day}. {MonthName(month)} {birthYear}"
            : $"{day}. {MonthName(month)}";
}
