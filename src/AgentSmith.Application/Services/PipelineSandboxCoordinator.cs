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
/// Per-repo ISandbox lifecycle for one pipeline run (p0158e). Each configured
/// repo gets its own sandbox with its own toolchain image, resolved via the
/// p0135 layered chain applied to THAT repo (not aggregated across repos).
///
/// Lifetime: transient / per-pipeline-run. Owns mutable state (the cached
/// per-repo sandboxes), so the DI registration MUST be transient — singleton
/// would share sandboxes across overlapping pipelines.
/// </summary>
public sealed class PipelineSandboxCoordinator(
    ISandboxFactory sandboxFactory,
    SandboxSpecBuilder sandboxSpecBuilder,
    ISandboxLanguageResolver sandboxLanguageResolver,
    ILogger<PipelineSandboxCoordinator> logger) : IPipelineSandboxCoordinator
{
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

    private readonly Dictionary<string, ISandbox> _sandboxes = new(StringComparer.Ordinal);
    private bool _disposed;

    public bool IsSandboxRequiring(string commandName) =>
        SandboxRequiringCommands.Contains(commandName);

    public bool RequiresSandbox(IEnumerable<PipelineCommand> commands) =>
        commands.Any(c => SandboxRequiringCommands.Contains(c.Name));

    public async Task<IReadOnlyDictionary<string, ISandbox>> EnsureSandboxesAsync(
        ResolvedProject projectConfig, PipelineContext context, CancellationToken cancellationToken)
    {
        var repos = context.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        foreach (var repo in repos)
        {
            if (_sandboxes.ContainsKey(repo.Name)) continue;
            _sandboxes[repo.Name] = await CreateOneAsync(projectConfig, repo, context, cancellationToken);
        }
        context.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, _sandboxes);
        context.Set(ContextKeys.Sandbox, _sandboxes[repos[0].Name]);
        return _sandboxes;
    }

    private async Task<ISandbox> CreateOneAsync(
        ResolvedProject projectConfig, RepoConnection repo, PipelineContext context, CancellationToken ct)
    {
        var (language, layer) = await ResolveToolchainAsync(projectConfig, repo, ct);
        var spec = sandboxSpecBuilder.Build(projectConfig, language);
        if (context.TryGet<string>(ContextKeys.SourcePath, out var hostSourcePath)
            && !string.IsNullOrEmpty(hostSourcePath))
            spec = spec with { InitialSourcePath = hostSourcePath };
        logger.LogInformation(
            "Sandbox for {Repo} toolchain via {Layer}: language={Language}, image={Image}",
            repo.Name, layer, language ?? "<none>", spec.ToolchainImage);
        var sandbox = await sandboxFactory.CreateAsync(spec, ct);
        logger.LogInformation("Sandbox for {Repo} published (image={Image})", repo.Name, spec.ToolchainImage);
        return sandbox;
    }

    private async Task<(string? Language, SandboxToolchainResolutionLayer Layer)> ResolveToolchainAsync(
        ResolvedProject projectConfig, RepoConnection repo, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(projectConfig.Sandbox?.ToolchainImage))
            return (null, SandboxToolchainResolutionLayer.Override);

        var result = await sandboxLanguageResolver.ResolveAsync(repo, ct);
        return (result.Language, result.Layer);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var sandbox in _sandboxes.Values)
        {
            if (sandbox is null) continue;
            await sandbox.DisposeAsync();
        }
        _sandboxes.Clear();
    }
}
