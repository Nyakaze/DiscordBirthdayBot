using Microsoft.EntityFrameworkCore;

namespace SadBot.Data;

public sealed class SadBotDbContext(DbContextOptions<SadBotDbContext> options) : DbContext(options)
{
    public DbSet<BirthdaySettings> BirthdaySettings => Set<BirthdaySettings>();

    public DbSet<Birthday> Birthdays => Set<Birthday>();

    public DbSet<Celebration> Celebrations => Set<Celebration>();

    public DbSet<BoardMessage> BoardMessages => Set<BoardMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<ulong>().HaveConversion<long>();
        configurationBuilder.Properties<ulong?>().HaveConversion<long?>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BirthdaySettings>(settings =>
        {
            settings.HasKey(entity => entity.GuildId);
            settings.Property(entity => entity.GuildId).ValueGeneratedNever();
            settings.Property(entity => entity.MessageTemplate).HasMaxLength(1500);
        });

        modelBuilder.Entity<Birthday>(birthday =>
        {
            birthday.HasIndex(entity => new { entity.GuildId, entity.UserId }).IsUnique();
            birthday.HasIndex(entity => entity.Month);
            birthday.Property(entity => entity.TimeZoneId).HasMaxLength(64);
        });

        modelBuilder.Entity<Celebration>(celebration =>
        {
            celebration.HasIndex(entity => new { entity.GuildId, entity.UserId, entity.Year }).IsUnique();
            celebration.HasIndex(entity => new { entity.Expired, entity.ExpiresAt });
        });

        modelBuilder.Entity<BoardMessage>(board =>
        {
            board.HasIndex(entity => new { entity.GuildId, entity.Page }).IsUnique();
        });
    }
}
