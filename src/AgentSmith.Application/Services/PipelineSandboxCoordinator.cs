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
/// Per-context ISandbox lifecycle for one pipeline run (p0158e + p0161a). Each
/// discovered .agentsmith/contexts/&lt;name&gt; gets its own sandbox with its own
/// toolchain image, resolved via the p0135 chain applied to THAT discovery's
/// language. Sandboxes dictionary is keyed by SandboxKeyComposer's composite
/// key. The parallel SandboxDiscoveries dictionary carries each sandbox's
/// Workdir + Language for handlers iterating sandbox keys.
///
/// Lifetime: transient / per-pipeline-run. Owns mutable state (the cached
/// per-key sandboxes + discoveries), so the DI registration MUST be transient.
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
        CommandNames.BootstrapDiscover, // p0161d: read-only LLM round reads via the per-repo sandbox
        CommandNames.LoadContext, CommandNames.LoadCodingPrinciples, CommandNames.LoadCodeMap,
        CommandNames.LoadRuns, CommandNames.AnalyzeCode,
        CommandNames.CompileDiscussion, CommandNames.CompileKnowledge, CommandNames.QueryKnowledge,
        CommandNames.WriteRunResult,
        CommandNames.StaticPatternScan, CommandNames.GitHistoryScan, CommandNames.DependencyAudit,
        CommandNames.SecurityTrend, CommandNames.SecuritySnapshotWrite, CommandNames.SpawnFix
    };

    private readonly Dictionary<string, ISandbox> _sandboxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RemoteContextDiscovery> _discoveries = new(StringComparer.Ordinal);
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
            var discoveries = await sandboxLanguageResolver.ResolveAllAsync(repo, cancellationToken);
            foreach (var discovery in discoveries)
                await EnsureOneAsync(projectConfig, repo, discovery, repos.Count, discoveries.Count, context, cancellationToken);
        }
        context.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, _sandboxes);
        context.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries, _discoveries);
        context.Set(ContextKeys.Sandbox, _sandboxes[_sandboxes.Keys.First()]);
        return _sandboxes;
    }

    private async Task EnsureOneAsync(
        ResolvedProject projectConfig, RepoConnection repo, RemoteContextDiscovery discovery,
        int repoCount, int perRepoDiscoveryCount, PipelineContext context, CancellationToken ct)
    {
        var key = SandboxKeyComposer.Compose(repoCount, repo.Name, perRepoDiscoveryCount, discovery.ContextName);
        if (_sandboxes.ContainsKey(key)) return;
        _sandboxes[key] = await CreateOneAsync(projectConfig, repo, discovery, key, context, ct);
        _discoveries[key] = discovery;
    }

    private async Task<ISandbox> CreateOneAsync(
        ResolvedProject projectConfig, RepoConnection repo, RemoteContextDiscovery discovery,
        string key, PipelineContext context, CancellationToken ct)
    {
        var spec = sandboxSpecBuilder.Build(projectConfig, discovery.Language);
        if (context.TryGet<string>(ContextKeys.SourcePath, out var hostSourcePath)
            && !string.IsNullOrEmpty(hostSourcePath))
            spec = spec with { InitialSourcePath = hostSourcePath };
        logger.LogInformation(
            "Sandbox {Key} for {Repo}/{Ctx} (workdir={Workdir}): language={Language}, image={Image}",
            key, repo.Name, discovery.ContextName, discovery.Workdir,
            discovery.Language ?? "<none>", spec.ToolchainImage);
        var sandbox = await sandboxFactory.CreateAsync(spec, ct);
        logger.LogInformation("Sandbox {Key} published (image={Image})", key, spec.ToolchainImage);
        return sandbox;
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
        _discoveries.Clear();
    }
}
