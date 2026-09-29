using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Crovus.Client;
using Crovus.Models;
using Crovus.Rest;
using Microsoft.EntityFrameworkCore;
using SadBot.Data;

namespace SadBot.Modules.Birthdays;

public sealed class BoardRefreshQueue
{
    private readonly Channel<ulong> _pending = Channel.CreateUnbounded<ulong>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public void Enqueue(ulong guildId) => _pending.Writer.TryWrite(guildId);

    public ChannelReader<ulong> Reader => _pending.Reader;
}

public sealed class BirthdayBoard(
    IDbContextFactory<SadBotDbContext> contexts,
    CrovusClient client,
    TimeProvider time,
    ILogger<BirthdayBoard> logger)
{
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _locks = new();

    public async Task<IReadOnlyList<DiscordEmbed>> RenderAsync(ulong guildId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var celebrating = await db.Celebrations
            .Where(celebration => celebration.GuildId == guildId && !celebration.Expired)
            .Select(celebration => celebration.UserId)
            .ToListAsync(cancellationToken);

        var birthdays = await db.Birthdays
            .Where(birthday => birthday.GuildId == guildId)
            .Select(birthday => new { birthday.UserId, birthday.Month, birthday.Day })
            .ToListAsync(cancellationToken);

        var celebratingSet = celebrating.ToHashSet();

        return BirthdayBoardRenderer.Render(
            birthdays.Select(birthday => new BoardEntry(birthday.UserId, birthday.Month, birthday.Day,
                celebratingSet.Contains(birthday.UserId))),
            time.GetUtcNow());
    }

    public async Task PublishAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);

            var previous = await db.BoardMessages.Where(board => board.GuildId == guildId)
                .ToListAsync(cancellationToken);

            foreach (var message in previous)
                await TryDeleteAsync(message, cancellationToken);

            db.BoardMessages.RemoveRange(previous);
            await db.SaveChangesAsync(cancellationToken);

            var pages = await RenderAsync(guildId, cancellationToken);

            for (var page = 0; page < pages.Count; page++)
            {
                var sent = await SendPageAsync(channelId, pages[page], cancellationToken);

                db.BoardMessages.Add(new BoardMessage
                {
                    GuildId = guildId, ChannelId = channelId, MessageId = sent.Id, Page = page
                });
            }

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Published the birthday board with {PageCount} pages in guild {GuildId}",
                pages.Count, guildId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RefreshAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);

            var messages = await db.BoardMessages
                .Where(board => board.GuildId == guildId)
                .OrderBy(board => board.Page)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
                return;

            var channelId = messages[0].ChannelId;
            var pages = await RenderAsync(guildId, cancellationToken);

            for (var page = 0; page < pages.Count; page++)
            {
                var existing = messages.FirstOrDefault(message => message.Page == page);

                if (existing is not null && await TryEditAsync(existing, pages[page], cancellationToken))
                    continue;

                var sent = await SendPageAsync(channelId, pages[page], cancellationToken);

                if (existing is null)
                    db.BoardMessages.Add(new BoardMessage
                    {
                        GuildId = guildId, ChannelId = channelId, MessageId = sent.Id, Page = page
                    });
                else
                    existing.MessageId = sent.Id;
            }

            foreach (var surplus in messages.Where(message => message.Page >= pages.Count))
            {
                await TryDeleteAsync(surplus, cancellationToken);
                db.BoardMessages.Remove(surplus);
            }

            await db.SaveChangesAsync(cancellationToken);

            logger.LogDebug("Refreshed the birthday board of guild {GuildId}", guildId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RemoveAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);

            var messages = await db.BoardMessages.Where(board => board.GuildId == guildId)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
                await TryDeleteAsync(message, cancellationToken);

            db.BoardMessages.RemoveRange(messages);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<DiscordMessage> SendPageAsync(ulong channelId, DiscordEmbed page,
        CancellationToken cancellationToken) =>
        client.Messages.SendAsync(channelId,
            new MessageCreateRequest(Embeds: [page], AllowedMentions: DiscordAllowedMentions.None),
            cancellationToken);

    private async Task<bool> TryEditAsync(BoardMessage message, DiscordEmbed page,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.Messages.EditAsync(message.ChannelId, message.MessageId,
                new MessageEditRequest(Embeds: [page], AllowedMentions: DiscordAllowedMentions.None),
                cancellationToken);

            return true;
        }
        catch (DiscordRestException exception) when (exception.StatusCode is HttpStatusCode.NotFound)
        {
            logger.LogInformation("Board message {MessageId} of guild {GuildId} is gone, posting it again",
                message.MessageId, message.GuildId);

            return false;
        }
    }

    private async Task TryDeleteAsync(BoardMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await client.Messages.DeleteAsync(message.ChannelId, message.MessageId,
                cancellationToken: cancellationToken);
        }
        catch (DiscordRestException exception) when (exception.StatusCode is HttpStatusCode.NotFound
                                                          or HttpStatusCode.Forbidden)
        {
            logger.LogDebug("Could not delete board message {MessageId}: {Status}", message.MessageId,
                exception.StatusCode);
        }
    }
}

public sealed class BoardRefreshWorker(
    BoardRefreshQueue queue,
    BirthdayBoard board,
    IDbContextFactory<SadBotDbContext> contexts,
    ILogger<BoardRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnqueueAllBoardsAsync(stoppingToken);

        var reader = queue.Reader;

        while (await reader.WaitToReadAsync(stoppingToken))
        {
            await Task.Delay(Debounce, stoppingToken);

            var guilds = new HashSet<ulong>();

            while (reader.TryRead(out var guildId))
                guilds.Add(guildId);

            foreach (var guildId in guilds)
            {
                try
                {
                    await board.RefreshAsync(guildId, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Could not refresh the birthday board of guild {GuildId}", guildId);
                }
            }
        }
    }

    private async Task EnqueueAllBoardsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);

            var guilds = await db.BoardMessages.Select(board => board.GuildId).Distinct()
                .ToListAsync(cancellationToken);

            foreach (var guildId in guilds)
                queue.Enqueue(guildId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not load the birthday boards to refresh on startup");
        }
    }
}
