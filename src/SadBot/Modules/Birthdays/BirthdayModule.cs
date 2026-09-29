using SadBot.Discord;

namespace SadBot.Modules.Birthdays;

public static class BirthdayModule
{
    public static IServiceCollection AddBirthdays(this IServiceCollection services)
    {
        services.AddSingleton<TimeZoneCatalog>();
        services.AddSingleton<BoardRefreshQueue>();
        services.AddSingleton<BirthdayService>();
        services.AddSingleton<BirthdayBoard>();
        services.AddSingleton<ICommandModule, BirthdayCommands>();
        services.AddSingleton<ICommandModule, BirthdayAdminCommands>();
        services.AddHostedService<BirthdayScheduler>();
        services.AddHostedService<BoardRefreshWorker>();

        return services;
    }
}
