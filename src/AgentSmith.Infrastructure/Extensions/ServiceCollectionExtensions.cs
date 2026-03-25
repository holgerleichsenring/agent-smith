using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core;
using AgentSmith.Infrastructure.Services.Containers;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Nuclei;
using AgentSmith.Infrastructure.Services.Spectral;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Infrastructure.Services.Providers;
using AgentSmith.Infrastructure.Services.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        services.AddKeyedSingleton<IOutputStrategy, SummaryOutputStrategy>("summary");
        services.AddKeyedSingleton<IOutputStrategy, SarifOutputStrategy>("sarif");
        services.AddKeyedSingleton<IOutputStrategy, MarkdownOutputStrategy>("markdown");

        services.AddSingleton<ISwaggerProvider, SwaggerProvider>();

        // Legacy IContainerRunner (still used by Dispatcher via DockerJobSpawner)
        services.AddSingleton<IContainerRunner, DockerContainerRunner>();

        // Tool runner (Nuclei, Spectral) — selected by config or auto-detected
        var toolRunnerConfig = LoadToolRunnerConfig();
        services.AddSingleton(toolRunnerConfig);
        services.AddSingleton<IToolRunner>(sp => CreateToolRunner(toolRunnerConfig, sp));

        services.AddSingleton(_ => LoadNucleiConfig());
        services.AddSingleton<INucleiScanner, NucleiSpawner>();
        services.AddSingleton<ISpectralScanner, SpectralSpawner>();

        return services;
    }

    internal static IToolRunner CreateToolRunner(ToolRunnerConfig config, IServiceProvider sp)
    {
        var type = config.Type.ToLowerInvariant();

        if (type == "auto")
            type = DetectToolRunnerType();

        return type switch
        {
            "docker" or "podman" => new DockerToolRunner(config,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                    .CreateLogger<DockerToolRunner>()),
            "process" => new ProcessToolRunner(
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                    .CreateLogger<ProcessToolRunner>()),
            _ => new DockerToolRunner(config,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                    .CreateLogger<DockerToolRunner>()),
        };
    }

    internal static string DetectToolRunnerType()
    {
        // Check for Docker socket
        if (File.Exists("/var/run/docker.sock"))
            return "docker";

        // Check for Podman socket (rootful)
        if (File.Exists("/run/podman/podman.sock"))
            return "podman";

        // Check for Podman socket (rootless)
        var uid = Environment.GetEnvironmentVariable("UID") ?? "1000";
        if (File.Exists($"/run/user/{uid}/podman/podman.sock"))
            return "podman";

        // Fallback to process
        return "process";
    }

    private static ToolRunnerConfig LoadToolRunnerConfig()
    {
        var path = Path.Combine("config", "agentsmith.yml");
        if (!File.Exists(path))
            return new ToolRunnerConfig();

        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var wrapper = deserializer.Deserialize<ToolRunnerConfigWrapper>(yaml);
            return wrapper?.ToolRunner ?? new ToolRunnerConfig();
        }
        catch
        {
            return new ToolRunnerConfig();
        }
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

    private sealed class ToolRunnerConfigWrapper
    {
        public ToolRunnerConfig? ToolRunner { get; set; }
    }
}
