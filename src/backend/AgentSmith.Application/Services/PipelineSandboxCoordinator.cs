using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
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
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
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
    private string? _runId;
    private bool _disposed;

    public bool IsSandboxRequiring(string commandName) =>
        SandboxRequiringCommands.Contains(commandName);

    public bool RequiresSandbox(IEnumerable<PipelineCommand> commands) =>
        commands.Any(c => SandboxRequiringCommands.Contains(c.Name));

    public async Task<IReadOnlyDictionary<string, ISandbox>> EnsureSandboxesAsync(
        ResolvedProject projectConfig, PipelineContext context, CancellationToken cancellationToken)
    {
        _runId ??= context.TryGet<string>(ContextKeys.RunId, out var rid) ? rid : null;
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
        var sandbox = await CreateOneAsync(projectConfig, repo, discovery, key, context, ct);
        _sandboxes[key] = sandbox;
        _discoveries[key] = discovery;
        await PublishCreatedAsync(key, discovery, projectConfig, ct);
    }

    private Task PublishCreatedAsync(
        string sandboxKey, RemoteContextDiscovery discovery, ResolvedProject projectConfig, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_runId)) return Task.CompletedTask;
        var image = sandboxSpecBuilder.Build(projectConfig, discovery.Language).ToolchainImage;
        return eventPublisher.PublishAsync(
            new SandboxCreatedEvent(_runId!, sandboxKey, image, discovery.Language, DateTimeOffset.UtcNow), ct);
    }

    private async Task<ISandbox> CreateOneAsync(
        ResolvedProject projectConfig, RepoConnection repo, RemoteContextDiscovery discovery,
        string key, PipelineContext context, CancellationToken ct)
    {
        var spec = sandboxSpecBuilder.Build(projectConfig, discovery.Language);
        if (context.TryGet<string>(ContextKeys.SourcePath, out var hostSourcePath)
            && !string.IsNullOrEmpty(hostSourcePath))
            spec = spec with { InitialSourcePath = hostSourcePath };
        var languageTag = discovery.Language ?? "null (generic fallback)";
        logger.LogInformation(
            "Sandbox {Key}/{Ctx}: lang={Language} image={Image} workdir={Workdir}",
            key, discovery.ContextName, languageTag, spec.ToolchainImage, discovery.Workdir);
        var sandbox = await sandboxFactory.CreateAsync(spec, ct);
        logger.LogInformation("Sandbox {Key} published (image={Image})", key, spec.ToolchainImage);
        // p0169e: wrap with the seam-side projector so each RunStepAsync emits
        // SandboxCommand/Output/Result. Sandbox.Agent + Wire remain untouched.
        // If the factory returned null (test stub paths), pass through — the
        // disposer's null guard already covers that branch.
        return sandbox is null ? null! : new SandboxEventProjector(sandbox, eventPublisher, runContext, key);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (key, sandbox) in _sandboxes)
        {
            if (sandbox is null) continue;
            await sandbox.DisposeAsync();
            await PublishDisposedAsync(key);
        }
        _sandboxes.Clear();
        _discoveries.Clear();
    }

    private async Task PublishDisposedAsync(string sandboxKey)
    {
        if (string.IsNullOrEmpty(_runId)) return;
        try
        {
            await eventPublisher.PublishAsync(
                new SandboxDisposedEvent(_runId!, sandboxKey, ExitCode: null, DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to publish SandboxDisposed for {Key}", sandboxKey);
        }
    }
}
