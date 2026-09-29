using Crovus.Client;
using Crovus.Events;
using Crovus.Logs;
using Crovus.Models;
using Microsoft.Extensions.Options;

namespace SadBot.Discord;

public sealed class DiscordBotService(
    CrovusClient client,
    IEnumerable<ICommandModule> modules,
    IOptions<DiscordOptions> options,
    ILoggerFactory loggerFactory,
    ILogger<DiscordBotService> logger) : BackgroundService
{
    private readonly Dictionary<string, ICommandModule> _modules =
        modules.ToDictionary(module => module.Command.Build().Name, StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var forwarding = client.Diagnostics.ForwardLogsTo(loggerFactory);
        using var ready = client.On<ReadyEvent>((ready, cancellationToken) =>
            SynchronizeCommandsAsync(ready, cancellationToken));
        using var interactions = client.On<InteractionCreatedEvent>((created, cancellationToken) =>
            HandleInteractionAsync(created.Interaction, cancellationToken));

        try
        {
            await client.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SynchronizeCommandsAsync(ReadyEvent ready, CancellationToken cancellationToken)
    {
        if (ready.ApplicationId is not { } applicationId)
        {
            logger.LogWarning("Ready event carried no application id, skipping command registration");
            return;
        }

        var commands = _modules.Values.Select(module => module.Command).ToArray();
        Snowflake? guildId = options.Value.DevelopmentGuildId is { } id ? new Snowflake(id) : null;

        var result = await client.Commands.SynchronizeAsync(applicationId, commands, guildId, cancellationToken);

        logger.LogInformation(
            "Synchronized {CommandCount} commands (added {Added}, changed {Changed}, removed {Removed})",
            commands.Length, result.Added, result.Changed, result.Removed);
    }

    private async Task HandleInteractionAsync(DiscordInteraction interaction, CancellationToken cancellationToken)
    {
        if (!_modules.TryGetValue(interaction.CommandName, out var module))
            return;

        try
        {
            switch (interaction.Type)
            {
                case InteractionType.ApplicationCommand:
                    await module.ExecuteAsync(interaction, cancellationToken);
                    break;

                case InteractionType.ApplicationCommandAutocomplete:
                    await module.AutocompleteAsync(interaction, cancellationToken);
                    break;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Command {CommandPath} failed in guild {GuildId} for user {UserId}",
                interaction.CommandPath, interaction.GuildId?.Value, interaction.Invoker?.Id.Value);

            if (interaction.Type is InteractionType.ApplicationCommand)
                await TryReportFailureAsync(interaction, cancellationToken);
        }
    }

    private async Task TryReportFailureAsync(DiscordInteraction interaction, CancellationToken cancellationToken)
    {
        const string message = "Something went wrong while running this command. Please try again later.";

        try
        {
            await client.ReplyAsync(interaction, message, cancellationToken);
        }
        catch (Exception)
        {
            try
            {
                await client.EditReplyAsync(interaction, message, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "Could not report the failure of interaction {InteractionId}",
                    interaction.Id.Value);
            }
        }
    }
}
