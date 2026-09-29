using System.Collections.Concurrent;
using System.Net;
using Crovus.Client;
using Crovus.Rest;
using Microsoft.EntityFrameworkCore;
using SadBot.Data;
using SadBot.Gifs;

namespace SadBot.Modules.Birthdays;

public sealed class BirthdayScheduler(
    IDbContextFactory<SadBotDbContext> contexts,
    CrovusClient client,
    TimeZoneCatalog timeZones,
    IGifProvider gifs,
    BoardRefreshQueue boardRefresh,
    TimeProvider time,
    ILogger<BirthdayScheduler> logger) : BackgroundService
{
    public const string RoleName = "Birthday";

    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan CelebrationLength = TimeSpan.FromHours(24);
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId, int Year), DateTimeOffset> _retryAfter = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "The birthday scheduler tick failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await ExpireAsync(cancellationToken);
        await CelebrateDueAsync(cancellationToken);
    }

    private async Task ExpireAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var due = await db.Celebrations
            .Where(celebration => !celebration.Expired && celebration.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var celebration in due)
        {
            var finished = true;

            if (celebration is { ChannelId: { } channelId, MessageId: { } messageId })
                finished &= await TryCleanupAsync(
                    () => client.Messages.DeleteAsync(channelId, messageId, cancellationToken: cancellationToken),
                    "delete the celebration message", celebration);

            if (celebration.RoleId is { } roleId)
                finished &= await TryCleanupAsync(
                    () => client.Members.RevokeRoleAsync(celebration.GuildId, celebration.UserId, roleId,
                        "Birthday is over", cancellationToken),
                    "revoke the birthday role", celebration);

            if (!finished)
                continue;

            celebration.Expired = true;
            boardRefresh.Enqueue(celebration.GuildId);

            logger.LogInformation("Ended the birthday celebration of user {UserId} in guild {GuildId}",
                celebration.UserId, celebration.GuildId);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task CelebrateDueAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var months = BirthdayCalendar.CandidateMonths(now).ToArray();

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var settings = await db.BirthdaySettings
            .Where(entry => entry.AnnouncementChannelId != null)
            .ToDictionaryAsync(entry => entry.GuildId, cancellationToken);

        if (settings.Count == 0)
            return;

        var candidates = await db.Birthdays.AsNoTracking()
            .Where(birthday => months.Contains(birthday.Month))
            .ToListAsync(cancellationToken);

        foreach (var birthday in candidates)
        {
            if (!settings.TryGetValue(birthday.GuildId, out var guildSettings))
                continue;

            if (!timeZones.TryResolve(birthday.TimeZoneId, out _, out var zone))
            {
                logger.LogWarning("Unknown time zone {TimeZone} for user {UserId} in guild {GuildId}",
                    birthday.TimeZoneId, birthday.UserId, birthday.GuildId);
                continue;
            }

            var today = BirthdayCalendar.LocalToday(now, zone);

            if (!BirthdayCalendar.IsBirthday(today, birthday.Month, birthday.Day))
                continue;

            var key = (birthday.GuildId, birthday.UserId, today.Year);

            if (_retryAfter.TryGetValue(key, out var retryAfter) && retryAfter > now)
                continue;

            var celebrated = await db.Celebrations.AnyAsync(celebration =>
                celebration.GuildId == birthday.GuildId && celebration.UserId == birthday.UserId &&
                celebration.Year == today.Year, cancellationToken);

            if (celebrated)
                continue;

            try
            {
                await CelebrateAsync(db, birthday, guildSettings, today, now, cancellationToken);
                _retryAfter.TryRemove(key, out _);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _retryAfter[key] = now + RetryDelay;

                logger.LogWarning(exception,
                    "Could not celebrate user {UserId} in guild {GuildId}, retrying in {RetryMinutes} minutes",
                    birthday.UserId, birthday.GuildId, RetryDelay.TotalMinutes);
            }
        }
    }

    private async Task CelebrateAsync(SadBotDbContext db, Birthday birthday, BirthdaySettings settings,
        DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var celebration = new Celebration
        {
            GuildId = birthday.GuildId,
            UserId = birthday.UserId,
            Year = today.Year,
            CelebratedAt = now,
            ExpiresAt = now + CelebrationLength
        };

        string displayName;

        try
        {
            var member = await client.Members.GetAsync(birthday.GuildId, birthday.UserId, cancellationToken);
            displayName = member.DisplayName;
        }
        catch (DiscordRestException exception) when (exception.StatusCode is HttpStatusCode.NotFound)
        {
            logger.LogInformation("User {UserId} is no longer in guild {GuildId}, skipping this year's celebration",
                birthday.UserId, birthday.GuildId);

            celebration.Expired = true;
            db.Celebrations.Add(celebration);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var channelId = settings.AnnouncementChannelId!.Value;
        var gif = await gifs.GetRandomAsync(cancellationToken);
        var age = BirthdayCalendar.AgeOn(today, birthday.Year);

        var message = await client.Messages.SendAsync(channelId,
            CelebrationMessage.Build(settings.MessageTemplate, birthday.UserId, displayName, age, gif),
            cancellationToken);

        celebration.ChannelId = channelId;
        celebration.MessageId = message.Id;

        db.Celebrations.Add(celebration);
        await db.SaveChangesAsync(cancellationToken);

        celebration.RoleId = await TryGrantRoleAsync(db, settings, birthday.UserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        boardRefresh.Enqueue(birthday.GuildId);

        logger.LogInformation("Celebrated the birthday of user {UserId} in guild {GuildId}", birthday.UserId,
            birthday.GuildId);
    }

    private async Task<ulong?> TryGrantRoleAsync(SadBotDbContext db, BirthdaySettings settings, ulong userId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (settings.BirthdayRoleId is null)
            {
                var role = await client.Roles.EnsureAsync(settings.GuildId, RoleName,
                    reason: "Role for members celebrating their birthday", cancellationToken: cancellationToken);

                settings.BirthdayRoleId = role.Id;
                await db.SaveChangesAsync(cancellationToken);
            }

            var roleId = settings.BirthdayRoleId.Value;

            await client.Members.GrantRoleAsync(settings.GuildId, userId, roleId, "Happy birthday!",
                cancellationToken);

            return roleId;
        }
        catch (DiscordRestException exception) when (exception.StatusCode is HttpStatusCode.Forbidden
                                                         or HttpStatusCode.NotFound)
        {
            logger.LogWarning(exception,
                "Could not grant the birthday role in guild {GuildId}. Check that the bot has Manage Roles and " +
                "that its role is above the birthday role", settings.GuildId);

            return null;
        }
    }

    private async Task<bool> TryCleanupAsync(Func<Task> action, string what, Celebration celebration)
    {
        try
        {
            await action();
            return true;
        }
        catch (DiscordRestException exception) when (exception.StatusCode is HttpStatusCode.NotFound
                                                         or HttpStatusCode.Forbidden)
        {
            logger.LogInformation("Could not {Action} for user {UserId} in guild {GuildId}: {Status}", what,
                celebration.UserId, celebration.GuildId, exception.StatusCode);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Failed to {Action} for user {UserId} in guild {GuildId}, will retry",
                what, celebration.UserId, celebration.GuildId);
            return false;
        }
    }
}
