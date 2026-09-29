namespace SadBot.Data;

public sealed class BirthdaySettings
{
    public ulong GuildId { get; set; }

    public ulong? AnnouncementChannelId { get; set; }

    public ulong? BirthdayRoleId { get; set; }

    public string? MessageTemplate { get; set; }
}

public sealed class Birthday
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public ulong UserId { get; set; }

    public int Month { get; set; }

    public int Day { get; set; }

    public int? Year { get; set; }

    public required string TimeZoneId { get; set; }

    public ulong AddedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Celebration
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public ulong UserId { get; set; }

    public int Year { get; set; }

    public ulong? ChannelId { get; set; }

    public ulong? MessageId { get; set; }

    public ulong? RoleId { get; set; }

    public DateTimeOffset CelebratedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public bool Expired { get; set; }
}

public sealed class BoardMessage
{
    public int Id { get; set; }

    public ulong GuildId { get; set; }

    public ulong ChannelId { get; set; }

    public ulong MessageId { get; set; }

    public int Page { get; set; }
}
