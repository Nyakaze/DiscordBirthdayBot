using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SadBot.Data;
using SadBot.Modules.Birthdays;

namespace SadBot.Tests;

public sealed class BirthdayServiceTests : IAsyncLifetime
{
    private const ulong Guild = 1;

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly BoardRefreshQueue _queue = new();
    private readonly FixedTime _time = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
    private BirthdayService _service = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SadBotDbContext>().UseSqlite(_connection).Options;
        var factory = new ContextFactory(options);

        await using (var db = factory.CreateDbContext())
            await db.Database.EnsureCreatedAsync();

        _service = new BirthdayService(factory, new TimeZoneCatalog(), _queue, _time);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task CreatesThenUpdatesABirthday()
    {
        var created = await _service.SaveAsync(Guild, 10, 3, 15, null, "europe/berlin", 10, CancellationToken.None);
        var updated = await _service.SaveAsync(Guild, 10, 4, 1, 1990, "UTC", 99, CancellationToken.None);

        Assert.Equal(SaveBirthdayStatus.Created, created.Status);
        Assert.Equal("Europe/Berlin", created.Birthday!.TimeZoneId);
        Assert.Equal(SaveBirthdayStatus.Updated, updated.Status);

        var stored = await _service.FindAsync(Guild, 10, CancellationToken.None);

        Assert.Equal((4, 1, 1990, 99UL), (stored!.Month, stored.Day, stored.Year, stored.AddedBy));
        Assert.True(_queue.Reader.TryRead(out var guild));
        Assert.Equal(Guild, guild);
    }

    [Fact]
    public async Task RejectsInvalidInput()
    {
        var badDate = await _service.SaveAsync(Guild, 10, 2, 30, null, "UTC", 10, CancellationToken.None);
        var badZone = await _service.SaveAsync(Guild, 10, 2, 1, null, "Nowhere/Land", 10, CancellationToken.None);

        Assert.Equal(SaveBirthdayStatus.InvalidDate, badDate.Status);
        Assert.Equal(SaveBirthdayStatus.InvalidTimeZone, badZone.Status);
        Assert.False(_queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task RemovesABirthday()
    {
        await _service.SaveAsync(Guild, 10, 3, 15, null, "UTC", 10, CancellationToken.None);

        Assert.True(await _service.RemoveAsync(Guild, 10, CancellationToken.None));
        Assert.False(await _service.RemoveAsync(Guild, 10, CancellationToken.None));
        Assert.Null(await _service.FindAsync(Guild, 10, CancellationToken.None));
    }

    [Fact]
    public async Task ListsUpcomingBirthdaysInOrder()
    {
        await _service.SaveAsync(Guild, 1, 1, 5, null, "UTC", 1, CancellationToken.None);
        await _service.SaveAsync(Guild, 2, 12, 24, null, "UTC", 2, CancellationToken.None);
        await _service.SaveAsync(Guild, 3, 9, 29, null, "UTC", 3, CancellationToken.None);
        await _service.SaveAsync(2, 4, 9, 30, null, "UTC", 4, CancellationToken.None);

        var upcoming = await _service.UpcomingAsync(Guild, 10, CancellationToken.None);

        Assert.Equal([3UL, 2UL, 1UL], upcoming.Select(birthday => birthday.UserId));
    }

    [Fact]
    public async Task StoresSettingsPerGuild()
    {
        await _service.UpdateSettingsAsync(Guild, settings => settings.AnnouncementChannelId = 555,
            CancellationToken.None);

        Assert.Equal(555UL, (await _service.GetSettingsAsync(Guild, CancellationToken.None)).AnnouncementChannelId);
        Assert.Null((await _service.GetSettingsAsync(2, CancellationToken.None)).AnnouncementChannelId);
    }

    private sealed class ContextFactory(DbContextOptions<SadBotDbContext> options)
        : IDbContextFactory<SadBotDbContext>
    {
        public SadBotDbContext CreateDbContext() => new(options);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
