using Crovus.Client;
using Crovus.Gateway;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SadBot.Data;
using SadBot.Discord;
using SadBot.Gifs;
using SadBot.Modules.Birthdays;
using SadBot.Modules.Moderation;

if (args is ["--healthcheck", ..])
    return await ProbeHealthAsync();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<DiscordOptions>()
    .BindConfiguration(DiscordOptions.Section)
    .Validate(options => !string.IsNullOrWhiteSpace(options.Token), "Discord:Token is required.")
    .ValidateOnStart();

builder.Services.AddOptions<GifOptions>().BindConfiguration(GifOptions.Section);

builder.Services.AddDbContextFactory<SadBotDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database"),
        npgsql => npgsql.EnableRetryOnFailure()));

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton(services =>
{
    var discord = services.GetRequiredService<IOptions<DiscordOptions>>().Value;
    var options = CrovusClientOptions.For(discord.Token, GatewayIntents.Guilds) with { EnableRestLogging = false };

    return new CrovusClient(options);
});

builder.Services.AddHttpClient<IGifProvider, GiphyGifProvider>(http => http.Timeout = TimeSpan.FromSeconds(5));

builder.Services.AddBirthdays();
builder.Services.AddModeration();
builder.Services.AddHostedService<DiscordBotService>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<DiscordGatewayHealthCheck>("discord", tags: ["ready"]);

var app = builder.Build();

await using (var db = await app.Services.GetRequiredService<IDbContextFactory<SadBotDbContext>>()
                 .CreateDbContextAsync())
{
    await db.Database.MigrateAsync();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();

return 0;

static async Task<int> ProbeHealthAsync()
{
    var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';', ',')[0] ?? "8080";

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

    try
    {
        using var response = await http.GetAsync($"http://localhost:{port}/health/ready");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception)
    {
        return 1;
    }
}
