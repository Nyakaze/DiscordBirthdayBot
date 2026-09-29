using Microsoft.EntityFrameworkCore;
using SadBot.Data;

namespace SadBot.Modules.Birthdays;

public enum SaveBirthdayStatus
{
    Created,
    Updated,
    InvalidDate,
    InvalidTimeZone
}

public sealed record SaveBirthdayResult(SaveBirthdayStatus Status, Birthday? Birthday = null);

public sealed class BirthdayService(
    IDbContextFactory<SadBotDbContext> contexts,
    TimeZoneCatalog timeZones,
    BoardRefreshQueue boardRefresh,
    TimeProvider time)
{
    public const string DefaultTemplate = "🎉 Happy Birthday {user}! 🎂";

    public async Task<SaveBirthdayResult> SaveAsync(ulong guildId, ulong userId, int month, int day, int? year,
        string timeZone, ulong addedBy, CancellationToken cancellationToken)
    {
        if (!timeZones.TryResolve(timeZone, out var timeZoneId, out var zone))
            return new SaveBirthdayResult(SaveBirthdayStatus.InvalidTimeZone);

        var now = time.GetUtcNow();

        if (!BirthdayCalendar.IsValid(month, day, year, BirthdayCalendar.LocalToday(now, zone)))
            return new SaveBirthdayResult(SaveBirthdayStatus.InvalidDate);

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var birthday = await db.Birthdays.SingleOrDefaultAsync(
            entry => entry.GuildId == guildId && entry.UserId == userId, cancellationToken);

        var created = birthday is null;

        birthday ??= db.Birthdays.Add(new Birthday { GuildId = guildId, UserId = userId, TimeZoneId = timeZoneId })
            .Entity;

        birthday.Month = month;
        birthday.Day = day;
        birthday.Year = year;
        birthday.TimeZoneId = timeZoneId;
        birthday.AddedBy = addedBy;
        birthday.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        boardRefresh.Enqueue(guildId);

        return new SaveBirthdayResult(created ? SaveBirthdayStatus.Created : SaveBirthdayStatus.Updated, birthday);
    }

    public async Task<bool> RemoveAsync(ulong guildId, ulong userId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var removed = await db.Birthdays
            .Where(entry => entry.GuildId == guildId && entry.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
            boardRefresh.Enqueue(guildId);

        return removed > 0;
    }

    public async Task<Birthday?> FindAsync(ulong guildId, ulong userId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        return await db.Birthdays.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.GuildId == guildId && entry.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Birthday>> UpcomingAsync(ulong guildId, int count,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var birthdays = await db.Birthdays.AsNoTracking()
            .Where(entry => entry.GuildId == guildId)
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow();

        return birthdays
            .OrderBy(entry => NextOccurrence(entry, now))
            .ThenBy(entry => entry.UserId)
            .Take(count)
            .ToArray();
    }

    public DateOnly NextOccurrence(Birthday birthday, DateTimeOffset now)
    {
        var zone = timeZones.TryResolve(birthday.TimeZoneId, out _, out var found) ? found : TimeZoneInfo.Utc;

        return BirthdayCalendar.NextOccurrence(BirthdayCalendar.LocalToday(now, zone), birthday.Month,
            birthday.Day);
    }

    public async Task<BirthdaySettings> GetSettingsAsync(ulong guildId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        return await db.BirthdaySettings.AsNoTracking()
                   .SingleOrDefaultAsync(settings => settings.GuildId == guildId, cancellationToken)
               ?? new BirthdaySettings { GuildId = guildId };
    }

    public async Task<BirthdaySettings> UpdateSettingsAsync(ulong guildId, Action<BirthdaySettings> update,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var settings = await db.BirthdaySettings
            .SingleOrDefaultAsync(entry => entry.GuildId == guildId, cancellationToken);

        settings ??= db.BirthdaySettings.Add(new BirthdaySettings { GuildId = guildId }).Entity;

        update(settings);

        await db.SaveChangesAsync(cancellationToken);

        return settings;
    }
}
