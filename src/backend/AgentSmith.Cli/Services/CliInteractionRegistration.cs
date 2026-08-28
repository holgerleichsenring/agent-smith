using AgentSmith.Application.Services;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Infrastructure.Services.Dialogue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Cli.Services;

/// <summary>
/// How a one-shot CLI run reaches whoever started it: the console when a person did, and
/// the server's Redis channels when the server spawned it as a job. One seam, chosen once,
/// so no verb has to know which of the two it is running under.
/// <para>
/// 2026-08-28-2af6: lifted out of ServiceProviderFactory, which was over the file-length
/// limit — this is a registration decision of its own, not part of building the graph.
/// </para>
/// </summary>
internal static class CliInteractionRegistration
{
    public static IServiceCollection AddCliInteraction(
        this IServiceCollection services, bool headless, string jobId, string redisUrl)
    {
        var spawnedJobMode = !string.IsNullOrWhiteSpace(jobId) && !string.IsNullOrWhiteSpace(redisUrl);
        if (spawnedJobMode) return AddSpawnedJobChannels(services, jobId, redisUrl);

        services.AddSingleton<IDialogueTransport>(sp =>
            new ConsoleDialogueTransport(
                Console.In, Console.Out,
                sp.GetRequiredService<ILogger<ConsoleDialogueTransport>>()));
        services.AddSingleton<IProgressReporter>(sp =>
            new ConsoleProgressReporter(
                sp.GetRequiredService<ILogger<ConsoleProgressReporter>>(), headless));
        return services;
    }

    private static IServiceCollection AddSpawnedJobChannels(
        IServiceCollection services, string jobId, string redisUrl)
    {
        services.AddSingleton<IConnectionMultiplexer>(ConnectMultiplexer(redisUrl));
        services.AddSingleton<IMessageBus, RedisMessageBus>();
        services.AddSingleton<IDialogueTransport, RedisDialogueTransport>();
        services.AddSingleton<IProgressReporter>(sp =>
            new RedisProgressReporter(
                sp.GetRequiredService<IMessageBus>(), jobId,
                sp.GetRequiredService<ILogger<RedisProgressReporter>>()));
        return services;
    }

    private static IConnectionMultiplexer ConnectMultiplexer(string redisUrl)
    {
        var options = ConfigurationOptions.Parse(redisUrl);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ConnectTimeout = 5000;
        return ConnectionMultiplexer.Connect(options);
    }
}
