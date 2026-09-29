using Crovus.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SadBot.Discord;

public sealed class DiscordGatewayHealthCheck(CrovusClient client) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(client.IsConnected
            ? HealthCheckResult.Healthy($"Connected with {client.Latency?.TotalMilliseconds:F0}ms latency.")
            : HealthCheckResult.Unhealthy($"The gateway is {client.State}."));
}
