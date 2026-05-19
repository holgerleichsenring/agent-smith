using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Nuclei;
using AgentSmith.Infrastructure.Services.Providers;
using AgentSmith.Infrastructure.Services.Spectral;
using AgentSmith.Infrastructure.Services.Zap;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Security scanner + API-security wiring. Tool runner (Nuclei, Spectral) selected by
/// config or auto-detected; static pattern + git-history + dependency-audit scanners
/// (p54); Swagger + API-session provider (p79). YAML config search order:
/// AGENTSMITH_CONFIG_DIR &gt; ./config/&lt;file&gt; &gt; ./&lt;file&gt; &gt;
/// AppContext.BaseDirectory/config/&lt;file&gt;. Missing file → default-constructed T.
/// </summary>
public static class SecurityScannersExtensions
{
    public static IServiceCollection AddSecurityScanners(this IServiceCollection services)
    {
        services.AddSingleton<ISwaggerProvider, SwaggerProvider>();
        var toolRunnerConfig = ToolRunnerSetup.LoadToolRunnerConfig();
        services.AddSingleton(toolRunnerConfig);
        services.AddSingleton<IToolRunner>(sp => ToolRunnerSetup.CreateToolRunner(toolRunnerConfig, sp));
        services.AddSingleton(_ => LoadYamlConfig<AgentSmith.Contracts.Models.NucleiConfig>("nuclei.yaml"));
        services.AddSingleton<INucleiScanner, NucleiSpawner>();
        services.AddSingleton<ISpectralScanner, SpectralSpawner>();
        services.AddSingleton(_ => LoadYamlConfig<AgentSmith.Contracts.Models.ZapConfig>("zap.yaml"));
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
        return services;
    }

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
