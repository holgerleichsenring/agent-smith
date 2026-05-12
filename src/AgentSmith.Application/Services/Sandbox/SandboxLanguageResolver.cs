using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Resolves the project's primary language before the sandbox is created so
/// PipelineExecutor.TryCreateSandboxAsync can pick a language-specific
/// toolchain image instead of the generic fallback. Two layers in order:
/// host-side project-map.json cache (fast, written by every prior
/// AnalyzeProjectHandler run), then remote .agentsmith/context.yaml read via
/// the source provider (covers cache-cold pods that have a prior init-project
/// merged into the repo). Returns null + GenericFallback when neither layer
/// produces a language.
/// </summary>
public sealed class SandboxLanguageResolver(
    IAgentSmithPaths paths,
    ISourceProviderFactory sourceProviderFactory,
    ILogger<SandboxLanguageResolver> logger) : ISandboxLanguageResolver
{
    private const string ContextYamlPath = ".agentsmith/context.yaml";
    private const string ProjectMapFileName = "project-map.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<ToolchainResolutionResult> ResolveAsync(
        SourceConfig source, CancellationToken cancellationToken)
    {
        var cacheLanguage = await TryResolveFromCacheAsync(source, cancellationToken);
        if (!string.IsNullOrEmpty(cacheLanguage))
            return new ToolchainResolutionResult(cacheLanguage, SandboxToolchainResolutionLayer.HostCache);

        var remoteLanguage = await TryResolveFromContextYamlAsync(source, cancellationToken);
        if (!string.IsNullOrEmpty(remoteLanguage))
            return new ToolchainResolutionResult(remoteLanguage, SandboxToolchainResolutionLayer.RemoteContextYaml);

        return new ToolchainResolutionResult(null, SandboxToolchainResolutionLayer.GenericFallback);
    }

    private async Task<string?> TryResolveFromCacheAsync(
        SourceConfig source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(source.Url)) return null;

        var cacheDir = paths.ProjectCacheDir(source.Url);
        var mapPath = Path.Combine(cacheDir, ProjectMapFileName);
        if (!File.Exists(mapPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(mapPath, cancellationToken);
            var map = JsonSerializer.Deserialize<ProjectMap>(json, JsonOptions);
            return string.IsNullOrEmpty(map?.PrimaryLanguage) ? null : map.PrimaryLanguage;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Stale/corrupt cache files are not worth failing the pipeline for —
            // AnalyzeProjectHandler will rewrite the cache on its next run.
            logger.LogDebug(ex, "project-map.json cache at {Path} unreadable, falling through", mapPath);
            return null;
        }
    }

    private async Task<string?> TryResolveFromContextYamlAsync(
        SourceConfig source, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(source.Url)) return null;

        string? yaml;
        try
        {
            var provider = sourceProviderFactory.Create(source);
            yaml = await provider.TryReadFileAsync(ContextYamlPath, cancellationToken);
        }
        catch (Exception ex)
        {
            // Auth / server / transport failures are non-fatal at this layer;
            // the pipeline can still run on the generic toolchain. We log so
            // operators can correlate a surprising fallback with infra trouble.
            logger.LogWarning(ex,
                "Remote .agentsmith/context.yaml read failed for {Url}, falling through to generic", source.Url);
            return null;
        }

        if (string.IsNullOrEmpty(yaml)) return null;

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var doc = deserializer.Deserialize<ContextYamlShape>(yaml);
            return string.IsNullOrEmpty(doc?.Stack?.Lang) ? null : doc.Stack.Lang;
        }
        catch (Exception ex) when (ex is YamlDotNet.Core.YamlException or InvalidCastException)
        {
            // Malformed YAML or stack.lang non-scalar (list / map) → graceful miss.
            logger.LogDebug(ex,
                "Remote .agentsmith/context.yaml for {Url} did not parse to the expected shape", source.Url);
            return null;
        }
    }

    // Minimal shape for the YAML deserializer — we read only stack.lang.
    // IgnoreUnmatchedProperties on the deserializer lets the rest of the
    // schema (meta, arch, behavior, ...) pass through silently.
    private sealed class ContextYamlShape
    {
        public StackBlock? Stack { get; set; }
    }

    private sealed class StackBlock
    {
        public string? Lang { get; set; }
    }
}
