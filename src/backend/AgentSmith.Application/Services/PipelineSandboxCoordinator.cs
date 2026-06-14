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
    ISandboxLivenessSupervisor livenessSupervisor,
    ILogger<PipelineSandboxCoordinator> logger) : IPipelineSandboxCoordinator
{
    private static readonly HashSet<string> SandboxRequiringCommands = new(StringComparer.Ordinal)
    {
        CommandNames.CheckoutSource, CommandNames.AcquireSource,
        CommandNames.AgenticExecute,
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
    private readonly Dictionary<string, List<RemoteContextDiscovery>> _contextsBySandbox = new(StringComparer.Ordinal);
    // p0249: sandbox key -> owning repo name, recorded where the key is composed.
    // The authoritative repo->sandbox source so consumers never reverse-engineer
    // the repo from the composite key string.
    private readonly Dictionary<string, string> _sandboxRepos = new(StringComparer.Ordinal);
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
        // p0261: `--context NAME` pins every repo to one named context instead of
        // the per-repo discovery / synthetic-default fallback. Unset → unchanged.
        var contextOverride = context.TryGet<string>(ContextKeys.SourceContext, out var ctxName)
            && !string.IsNullOrWhiteSpace(ctxName) ? ctxName : null;
        var repos = context.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        foreach (var repo in repos)
        {
            var discoveries = contextOverride is null
                ? await sandboxLanguageResolver.ResolveAllAsync(repo, cancellationToken)
                : await sandboxLanguageResolver.ResolveContextAsync(repo, contextOverride, cancellationToken);
            // p0180: group by toolchain image. Multiple same-image discoveries
            // share one container; the contexts-by-sandbox map carries the
            // full list per sandbox for per-context probes.
            var groups = discoveries
                .GroupBy(d => sandboxSpecBuilder.Build(projectConfig, d.Language, d.ToolchainImage).ToolchainImage, StringComparer.Ordinal)
                .ToList();
            foreach (var group in groups)
                await EnsureOneGroupAsync(projectConfig, repo, group.ToList(),
                    repos.Count, groups.Count, context, cancellationToken);
        }
        context.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, _sandboxes);
        context.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, _sandboxRepos);
        context.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries, _discoveries);
        var contextsView = _contextsBySandbox.ToDictionary(
            kv => kv.Key, kv => (IReadOnlyList<RemoteContextDiscovery>)kv.Value, StringComparer.Ordinal);
        context.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts, contextsView);
        context.Set(ContextKeys.Sandbox, _sandboxes[_sandboxes.Keys.First()]);
        return _sandboxes;
    }

    private async Task EnsureOneGroupAsync(
        ResolvedProject projectConfig, RepoConnection repo,
        IReadOnlyList<RemoteContextDiscovery> discoveriesInGroup,
        int repoCount, int repoGroupCount,
        PipelineContext context, CancellationToken ct)
    {
        var representative = discoveriesInGroup[0];
        var langSlug = LangSlug(representative.Language);
        var key = SandboxKeyComposer.ComposeForGroup(repoCount, repo.Name, repoGroupCount, langSlug);
        // p0249: record the owning repo for this key the moment it is composed —
        // authoritative, so SandboxesForRepo never has to parse it back out.
        _sandboxRepos[key] = repo.Name;
        if (_sandboxes.ContainsKey(key))
        {
            // Defensive: same key arrived twice (shouldn't happen for distinct
            // image groups). Merge contexts to keep the per-sandbox list complete.
            foreach (var d in discoveriesInGroup)
                if (!_contextsBySandbox[key].Any(existing => string.Equals(existing.ContextName, d.ContextName, StringComparison.Ordinal)))
                    _contextsBySandbox[key].Add(d);
            return;
        }
        var sandbox = await CreateOneAsync(projectConfig, repo, representative, discoveriesInGroup, key, context, ct);
        _sandboxes[key] = sandbox;
        _discoveries[key] = representative;
        _contextsBySandbox[key] = discoveriesInGroup.ToList();
        StartLivenessWatcher(key, sandbox);
        await PublishCreatedAsync(key, representative, projectConfig, ct);
    }

    private void StartLivenessWatcher(string sandboxKey, ISandbox sandbox)
    {
        if (string.IsNullOrEmpty(_runId)) return;
        livenessSupervisor.Watch(_runId!, sandboxKey, sandbox);
    }

    private static string LangSlug(string? language) =>
        string.IsNullOrEmpty(language) ? "generic" : language.ToLowerInvariant().Replace(' ', '-');

    private Task PublishCreatedAsync(
        string sandboxKey, RemoteContextDiscovery discovery, ResolvedProject projectConfig, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_runId)) return Task.CompletedTask;
        var image = sandboxSpecBuilder.Build(projectConfig, discovery.Language, discovery.ToolchainImage).ToolchainImage;
        return eventPublisher.PublishAsync(
            new SandboxCreatedEvent(_runId!, sandboxKey, image, discovery.Language, DateTimeOffset.UtcNow), ct);
    }

    private async Task<ISandbox> CreateOneAsync(
        ResolvedProject projectConfig, RepoConnection repo, RemoteContextDiscovery representative,
        IReadOnlyList<RemoteContextDiscovery> discoveriesInGroup,
        string key, PipelineContext context, CancellationToken ct)
    {
        var spec = sandboxSpecBuilder.Build(projectConfig, representative.Language, representative.ToolchainImage);
        if (context.TryGet<string>(ContextKeys.SourcePath, out var hostSourcePath)
            && !string.IsNullOrEmpty(hostSourcePath))
            spec = spec with { InitialSourcePath = hostSourcePath };
        if (!string.IsNullOrEmpty(_runId)) spec = spec with { RunId = _runId };
        var languageTag = representative.Language ?? "null (generic fallback)";
        if (discoveriesInGroup.Count == 1)
        {
            logger.LogInformation(
                "Sandbox {Key}/{Ctx}: lang={Language} image={Image} workdir={Workdir}",
                key, representative.ContextName, languageTag, spec.ToolchainImage, representative.Workdir);
        }
        else
        {
            var contexts = string.Join(", ", discoveriesInGroup.Select(d => $"{d.ContextName}@{d.Workdir}"));
            logger.LogInformation(
                "Sandbox {Key}: lang={Language} image={Image} shared by {Count} contexts [{Contexts}]",
                key, languageTag, spec.ToolchainImage, discoveriesInGroup.Count, contexts);
        }
        var sandbox = await sandboxFactory.CreateAsync(spec, ct);
        logger.LogInformation("Sandbox {Key} published (image={Image})", key, spec.ToolchainImage);
        return sandbox is null ? null! : new SandboxEventProjector(sandbox, eventPublisher, runContext, key);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        // p0201: stop watchers BEFORE disposing the sandboxes so a graceful
        // shutdown (heartbeat key deletion is part of HeartbeatLoop.Dispose)
        // never trips the watcher's "vanished" detection. Each sandbox's own
        // DisposeAsync sends the Shutdown step then deletes the heartbeat key.
        await livenessSupervisor.DisposeAsync();
        foreach (var (key, sandbox) in _sandboxes)
        {
            if (sandbox is null) continue;
            await sandbox.DisposeAsync();
            await PublishDisposedAsync(key);
        }
        _sandboxes.Clear();
        _discoveries.Clear();
        _contextsBySandbox.Clear();
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
