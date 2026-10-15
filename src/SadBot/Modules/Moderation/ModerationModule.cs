using SadBot.Discord;

namespace SadBot.Modules.Moderation;

public static class ModerationModule
{
    public static IServiceCollection AddModeration(this IServiceCollection services)
    {
        services.AddSingleton<ICommandModule, RenameCommands>();

        return services;
    }
}
