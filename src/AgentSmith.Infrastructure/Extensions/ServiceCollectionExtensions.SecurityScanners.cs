using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Containers;
using AgentSmith.Infrastructure.Services.Nuclei;
using AgentSmith.Infrastructure.Services.Providers;
using AgentSmith.Infrastructure.Services.Security;
using AgentSmith.Infrastructure.Services.Spectral;
using AgentSmith.Infrastructure.Services.Zap;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    // Security scanner + API-security wiring:
    // - Legacy IContainerRunner (still used by Dispatcher via DockerJobSpawner)
    // - Tool runner (Nuclei, Spectral) — selected by config or auto-detected
    // - Static pattern + git-history + dependency-audit scanners (p54)
    // - Swagger + API-session provider (p79)
    private static void AddSecurityScanners(IServiceCollection services)
    {
        services.AddSingleton<ISwaggerProvider, SwaggerProvider>();
        services.AddSingleton<IContainerRunner, DockerContainerRunner>();
        var toolRunnerConfig = ToolRunnerSetup.LoadToolRunnerConfig();
        services.AddSingleton(toolRunnerConfig);
        services.AddSingleton<IToolRunner>(sp => ToolRunnerSetup.CreateToolRunner(toolRunnerConfig, sp));
        services.AddSingleton(_ => LoadNucleiConfig());
        services.AddSingleton<INucleiScanner, NucleiSpawner>();
        services.AddSingleton<ISpectralScanner, SpectralSpawner>();
        services.AddSingleton(_ => LoadZapConfig());
        services.AddSingleton<IZapScanner, ZapSpawner>();
        services.AddSingleton<PatternDefinitionLoader>();
        services.AddSingleton<PatternsDirectoryResolver>();
        services.AddSingleton<PatternCompiler>();
        services.AddTransient<PatternFileMatcher>();
        services.AddTransient<GitDiffSecretMatcher>();
        services.AddSingleton<IStaticPatternScanner, StaticPatternScanner>();
        services.AddSingleton<IGitHistoryScanner, GitHistoryScanner>();
        services.AddSingleton<IDependencyAuditor, DependencyAuditor>();
        services.AddSingleton<ISessionProvider, SessionProvider>();
    }
}
