using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Output strategies (keyed by ProviderType for IOutputStrategy resolution). Each
/// pipeline preset picks its renderer at run-time via the keyed lookup; the matching
/// strategy writes either to stdout, a summary file, a SARIF report, or a Markdown digest.
/// </summary>
public static class OutputStrategiesExtensions
{
    public static IServiceCollection AddOutputStrategies(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IOutputStrategy, ConsoleOutputStrategy>("console");
        services.AddKeyedSingleton<IOutputStrategy, SummaryOutputStrategy>("summary");
        services.AddKeyedSingleton<IOutputStrategy, SarifOutputStrategy>("sarif");
        services.AddKeyedSingleton<IOutputStrategy, MarkdownOutputStrategy>("markdown");
        return services;
    }
}
