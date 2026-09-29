namespace SadBot.Discord;

public sealed class DiscordOptions
{
    public const string Section = "Discord";

    public string Token { get; set; } = string.Empty;

    public ulong? DevelopmentGuildId { get; set; }
}
