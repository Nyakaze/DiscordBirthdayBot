using Crovus.Factory;
using Crovus.Models;

namespace SadBot.Discord;

public interface ICommandModule
{
    SlashCommandFactory Command { get; }

    Task ExecuteAsync(DiscordInteraction interaction, CancellationToken cancellationToken);

    Task AutocompleteAsync(DiscordInteraction interaction, CancellationToken cancellationToken) => Task.CompletedTask;
}
