using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Lazy ISandbox lifecycle for one pipeline run.
///
/// Concerns kept here:
///   - SandboxRequiringCommands membership predicate
///   - toolchain-language resolution (override → in-memory ProjectMap →
///     SandboxLanguageResolver disk/network)
///   - SandboxSpecBuilder.Build + InitialSourcePath handoff
///   - sandboxFactory.CreateAsync, ContextKeys.Sandbox publication
///   - idempotency: subsequent EnsureSandboxAsync calls return the cached instance
///   - DisposeAsync: dispose exactly once
///
/// Lifetime: <b>transient / per-pipeline-run</b>. Owns mutable state (the
/// cached sandbox), so the DI registration MUST be transient — singleton would
/// share one ISandbox across overlapping pipelines.
/// </summary>
public sealed class PipelineSandboxCoordinator(
    ISandboxFactory sandboxFactory,
    SandboxSpecBuilder sandboxSpecBuilder,
    ISandboxLanguageResolver sandboxLanguageResolver,
    ILogger<PipelineSandboxCoordinator> logger) : IPipelineSandboxCoordinator
{
    // Every command that touches the project tree goes through the sandbox.
    // TryCheckoutSource is intentionally NOT listed — its handler clones host-side
    // via IHostSourceCloner and never touches ISandbox; listing it would force
    // upfront sandbox creation before the handler runs, breaking the
    // InitialSourcePath handoff for the InProcessSandboxFactory.
    private static readonly HashSet<string> SandboxRequiringCommands = new(StringComparer.Ordinal)
    {
        CommandNames.CheckoutSource, CommandNames.AcquireSource,
        CommandNames.AgenticExecute, CommandNames.Test,
        CommandNames.GenerateTests, CommandNames.GenerateDocs,
        CommandNames.CommitAndPR, CommandNames.InitCommit, CommandNames.PersistWorkBranch,
        CommandNames.BootstrapProject, CommandNames.BootstrapDocument, CommandNames.BootstrapCheck,
        CommandNames.LoadContext, CommandNames.LoadCodingPrinciples, CommandNames.LoadCodeMap,
        CommandNames.LoadRuns, CommandNames.AnalyzeCode,
        CommandNames.CompileDiscussion, CommandNames.CompileKnowledge, CommandNames.QueryKnowledge,
        CommandNames.WriteRunResult,
        CommandNames.StaticPatternScan, CommandNames.GitHistoryScan, CommandNames.DependencyAudit,
        CommandNames.SecurityTrend, CommandNames.SecuritySnapshotWrite, CommandNames.SpawnFix
    };

    private ISandbox? _sandbox;
    private bool _disposed;

    public bool IsSandboxRequiring(string commandName) =>
        SandboxRequiringCommands.Contains(commandName);

    public bool RequiresSandbox(IEnumerable<Domain.Models.PipelineCommand> commands) =>
        commands.Any(c => SandboxRequiringCommands.Contains(c.Name));

    public async Task<ISandbox> EnsureSandboxAsync(
        ResolvedProject projectConfig, PipelineContext context, CancellationToken cancellationToken)
    {
        if (_sandbox is not null) return _sandbox;

        var (language, layer) = await ResolveToolchainLanguageAsync(projectConfig, context, cancellationToken);
        var spec = sandboxSpecBuilder.Build(projectConfig, language);
        // When TryCheckoutSourceHandler (api-security-scan path) cloned the source host-side,
        // attach the path so InProcessSandboxFactory uses it as workDir — otherwise handlers
        // reading from /work see an empty dir.
        if (context.TryGet<string>(ContextKeys.SourcePath, out var hostSourcePath) && !string.IsNullOrEmpty(hostSourcePath))
            spec = spec with { InitialSourcePath = hostSourcePath };
        logger.LogInformation("Sandbox toolchain resolved via {Layer}: language={Language}, image={Image}",
            layer, language ?? "<none>", spec.ToolchainImage);
        _sandbox = await sandboxFactory.CreateAsync(spec, cancellationToken);
        context.Set(ContextKeys.Sandbox, _sandbox);
        logger.LogInformation("Sandbox published to pipeline context (image={Image})", spec.ToolchainImage);
        return _sandbox;
    }

    // Walk the resolution layers in priority order. Override and InMemoryProjectMap
    // are checked inline (they don't need the resolver's disk/network calls); the
    // cache + remote-context-yaml layers go through SandboxLanguageResolver.
    private async Task<(string? Language, SandboxToolchainResolutionLayer Layer)> ResolveToolchainLanguageAsync(
        ResolvedProject projectConfig, PipelineContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(projectConfig.Sandbox?.ToolchainImage))
        {
            // Builder consumes the override directly via ResolvedProject.Sandbox.ToolchainImage;
            // language stays null because the override is image-level.
            return (null, SandboxToolchainResolutionLayer.Override);
        }

        if (context.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var inMemory)
            && !string.IsNullOrEmpty(inMemory?.PrimaryLanguage))
        {
            return (inMemory.PrimaryLanguage, SandboxToolchainResolutionLayer.InMemoryProjectMap);
        }

        var repos = context.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        foreach (var repo in repos)
        {
            var result = await sandboxLanguageResolver.ResolveAsync(repo, cancellationToken);
            if (!string.IsNullOrEmpty(result.Language))
                return (result.Language, result.Layer);
        }
        return (null, SandboxToolchainResolutionLayer.GenericFallback);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_sandbox is not null)
        {
            await _sandbox.DisposeAsync();
            _sandbox = null;
        }
    }
}
