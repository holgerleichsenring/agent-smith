using AgentSmith.Application.Services;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Dialogue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// How a one-shot CLI run reaches the person who started it: dialogue and progress on the
/// console.
/// <para>
/// 2026-08-28-2af6: lifted out of ServiceProviderFactory, which was over the file-length
/// limit — this is a registration decision of its own, not part of building the graph.
/// </para>
/// </summary>
internal static class CliInteractionRegistration
{
    public static IServiceCollection AddCliInteraction(
        this IServiceCollection services, bool headless)
    {
        services.AddSingleton<IDialogueTransport>(sp =>
            new ConsoleDialogueTransport(
                Console.In, Console.Out,
                sp.GetRequiredService<ILogger<ConsoleDialogueTransport>>()));
        services.AddSingleton<IProgressReporter>(sp =>
            new ConsoleProgressReporter(
                sp.GetRequiredService<ILogger<ConsoleProgressReporter>>(), headless));
        return services;
    }
}
