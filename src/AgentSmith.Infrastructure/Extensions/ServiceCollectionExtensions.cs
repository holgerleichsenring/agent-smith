using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Nuclei;
using AgentSmith.Infrastructure.Services.Spectral;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Infrastructure.Services.Providers;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure;

/// <summary>
/// Registers all infrastructure services (providers, factories, config) with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithInfrastructure(this IServiceCollection services)
    {
        services.AddAgentSmithCore();
        services.AddHttpClient();
        services.AddSingleton<ITicketProviderFactory, TicketProviderFactory>();
        services.AddSingleton<ISourceProviderFactory, SourceProviderFactory>();
        services.AddSingleton<IAgentProviderFactory, AgentProviderFactory>();
        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();

        // Output strategies (keyed by ProviderType for IOutputStrategy resolution)
        services.AddKeyedSingleton<IOutputStrategy, ConsoleOutputStrategy>("console");
        services.AddKeyedSingleton<IOutputStrategy, SarifOutputStrategy>("sarif");
        services.AddKeyedSingleton<IOutputStrategy, MarkdownOutputStrategy>("markdown");

        services.AddSingleton<ISwaggerProvider, SwaggerProvider>();
        services.AddSingleton<IContainerRunner, AgentSmith.Infrastructure.Services.Containers.DockerContainerRunner>();
        services.AddSingleton(_ => LoadNucleiConfig());
        services.AddSingleton<INucleiScanner, NucleiSpawner>();
        services.AddSingleton<ISpectralScanner, SpectralSpawner>();

        return services;
    }

    private static NucleiConfig LoadNucleiConfig()
    {
        var path = Path.Combine("config", "nuclei.yaml");
        if (!File.Exists(path))
            return new NucleiConfig();

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<NucleiConfig>(yaml) ?? new NucleiConfig();
    }
}
