using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core;
using AgentSmith.Infrastructure.Services.Containers;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Nuclei;
using AgentSmith.Infrastructure.Services.Security;
using AgentSmith.Infrastructure.Services.Spectral;
using AgentSmith.Infrastructure.Services.Zap;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Infrastructure.Services.Providers;
using AgentSmith.Infrastructure.Services.Queue;
using AgentSmith.Infrastructure.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithInfrastructure(this IServiceCollection services)
    {
        services.AddAgentSmithCore();
        services.AddHttpClient();
        services.AddSingleton<ITicketProviderFactory, TicketProviderFactory>();
        services.AddSingleton<ITicketStatusTransitionerFactory, TicketStatusTransitionerFactory>();
        services.AddSingleton<Services.Providers.Tickets.JiraWorkflowCatalog>();
        services.AddSingleton<ISourceProviderFactory, SourceProviderFactory>();
        services.AddSingleton<IAgentProviderFactory, AgentProviderFactory>();
        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();

        // Redis-backed claim + queue primitives (p95a)
        services.AddSingleton<IRedisJobQueue, RedisJobQueue>();
        services.AddSingleton<IRedisClaimLock, RedisClaimLock>();

        // Output strategies (keyed by ProviderType for IOutputStrategy resolution)
        services.AddKeyedSingleton<IOutputStrategy, ConsoleOutputStrategy>("console");
        services.AddKeyedSingleton<IOutputStrategy, SummaryOutputStrategy>("summary");
        services.AddKeyedSingleton<IOutputStrategy, SarifOutputStrategy>("sarif");
        services.AddKeyedSingleton<IOutputStrategy, MarkdownOutputStrategy>("markdown");

        services.AddSingleton<ISwaggerProvider, SwaggerProvider>();

        // Legacy IContainerRunner (still used by Dispatcher via DockerJobSpawner)
        services.AddSingleton<IContainerRunner, DockerContainerRunner>();

        // Tool runner (Nuclei, Spectral) — selected by config or auto-detected
        var toolRunnerConfig = ToolRunnerSetup.LoadToolRunnerConfig();
        services.AddSingleton(toolRunnerConfig);
        services.AddSingleton<IToolRunner>(sp => ToolRunnerSetup.CreateToolRunner(toolRunnerConfig, sp));

        services.AddSingleton(_ => LoadNucleiConfig());
        services.AddSingleton<INucleiScanner, NucleiSpawner>();
        services.AddSingleton<ISpectralScanner, SpectralSpawner>();

        services.AddSingleton(_ => LoadZapConfig());
        services.AddSingleton<IZapScanner, ZapSpawner>();

        // Dialogue (p58)
        services.AddSingleton<IDialogueTransport, RedisDialogueTransport>();
        services.AddScoped<IDialogueTrail, InMemoryDialogueTrail>();

        // Security scanners (p54)
        services.AddSingleton<PatternDefinitionLoader>();
        services.AddSingleton<IStaticPatternScanner, StaticPatternScanner>();
        services.AddSingleton<IGitHistoryScanner, GitHistoryScanner>();
        services.AddSingleton<IDependencyAuditor, DependencyAuditor>();

        // API security probing (p79)
        services.AddSingleton<ISessionProvider, SessionProvider>();

        // PR comment reply and conversation lookup (p59, p59b, p59c)
        services.AddSingleton<IPrCommentReplyService, GitHubPrCommentReplyService>();
        services.AddKeyedSingleton<IPrCommentReplyService, GitHubPrCommentReplyService>("github");
        services.AddKeyedSingleton<IPrCommentReplyService, GitLabMrCommentReplyService>("gitlab");
        services.AddKeyedSingleton<IPrCommentReplyService, AzureDevOpsPrCommentReplyService>("azuredevops");
        services.AddSingleton<IConversationLookup, RedisConversationLookup>();

        return services;
    }

    private static NucleiConfig LoadNucleiConfig() =>
        LoadYamlConfig<NucleiConfig>("nuclei.yaml");

    private static ZapConfig LoadZapConfig() =>
        LoadYamlConfig<ZapConfig>("zap.yaml");

    private static T LoadYamlConfig<T>(string fileName) where T : new()
    {
        var path = FindConfigFile(fileName);
        if (path is null) return new T();

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<T>(yaml) ?? new T();
    }

    internal static string? FindConfigFile(string fileName)
    {
        var candidates = new List<string>
        {
            Path.Combine("config", fileName),
            fileName,
            Path.Combine(AppContext.BaseDirectory, "config", fileName),
        };

        var configDir = Environment.GetEnvironmentVariable("AGENTSMITH_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
        {
            candidates.Insert(0, Path.Combine(configDir, fileName));
            candidates.Insert(1, Path.Combine(configDir, "config", fileName));
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
