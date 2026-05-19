using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Output;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    // Output strategies (keyed by ProviderType for IOutputStrategy resolution). Each
    // pipeline preset picks its renderer at run-time via the keyed lookup; the
    // matching strategy writes either to stdout, a summary file, a SARIF report,
    // or a Markdown digest.
    private static void AddOutputStrategies(IServiceCollection services)
    {
        services.AddKeyedSingleton<IOutputStrategy, ConsoleOutputStrategy>("console");
        services.AddKeyedSingleton<IOutputStrategy, SummaryOutputStrategy>("summary");
        services.AddKeyedSingleton<IOutputStrategy, SarifOutputStrategy>("sarif");
        services.AddKeyedSingleton<IOutputStrategy, MarkdownOutputStrategy>("markdown");
    }
}
