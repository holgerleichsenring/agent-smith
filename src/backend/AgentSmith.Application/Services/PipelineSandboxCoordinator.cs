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
    // p0268: (repo, group-identity) -> sandbox key. Group identity is (image, resources);
    // this makes EnsureSandboxesAsync idempotent — a repeated call (or the same group
    // appearing twice) reuses the existing sandbox instead of creating a second — while
    // still letting two DISTINCT groups that compose the same key be disambiguated.
    private readonly Dictionary<string, string> _groupKeyToSandboxKey = new(StringComparer.Ordinal);
    private string? _runId;
    // p0320a: the run's pipeline name, read once from the PipelineContext so every
    // spec build sizes pipeline-aware (light profile for non-code-changing pipelines).
    private string? _pipelineName;
    private bool _disposed;

    public bool IsSandboxRequiring(string commandName) =>
        SandboxRequiringCommands.Contains(commandName);

    public bool RequiresSandbox(IEnumerable<PipelineCommand> commands) =>
        commands.Any(c => SandboxRequiringCommands.Contains(c.Name));

    public async Task<IReadOnlyDictionary<string, ISandbox>> EnsureSandboxesAsync(
        ResolvedProject projectConfig, PipelineContext context, CancellationToken cancellationToken)
    {
        _runId ??= context.TryGet<string>(ContextKeys.RunId, out var rid) ? rid : null;
        _pipelineName ??= context.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null;
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
            // p0268: group by (toolchain image, resources). Multiple discoveries
            // that share BOTH an image and a size share one container (a pod has one
            // resource spec); same-image-different-size contexts get separate pods.
            // The contexts-by-sandbox map carries the full list per sandbox for
            // per-context probes.
            var groups = discoveries
                .GroupBy(d => GroupKey(sandboxSpecBuilder.Build(
                        projectConfig, d.Language, _pipelineName, d.ToolchainImage, d.Resources)),
                    StringComparer.Ordinal)
                .ToList();
            // p0268: when two groups share a langSlug (same language, different size),
            // the key composer needs a resource slug to keep their sandbox keys distinct.
            var langGroupCounts = groups
                .GroupBy(g => LangSlug(g.First().Language), StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var groupList = group.ToList();
                var langSlug = LangSlug(groupList[0].Language);
                var resourceSlug = langGroupCounts[langSlug] > 1
                    ? ResourceSlug(sandboxSpecBuilder.Build(
                        projectConfig, groupList[0].Language, _pipelineName,
                        groupList[0].ToolchainImage, groupList[0].Resources).Resources)
                    : null;
                await EnsureOneGroupAsync(projectConfig, repo, groupList,
                    repos.Count, groups.Count, resourceSlug, context, cancellationToken);
            }
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
        int repoCount, int repoGroupCount, string? resourceSlug,
        PipelineContext context, CancellationToken ct)
    {
        var representative = discoveriesInGroup[0];
        var spec = sandboxSpecBuilder.Build(
            projectConfig, representative.Language, _pipelineName,
            representative.ToolchainImage, representative.Resources);
        var groupIdentity = $"{repo.Name}\n{GroupKey(spec)}";

        // p0268: same (repo, image, resources) seen again — a repeated EnsureSandboxesAsync
        // call or the same group twice. Reuse the cached sandbox (idempotent), merging any
        // new contexts into its per-sandbox list; never create a second pod.
        if (_groupKeyToSandboxKey.TryGetValue(groupIdentity, out var cachedKey))
        {
            MergeContexts(cachedKey, discoveriesInGroup);
            return;
        }

        var langSlug = LangSlug(representative.Language);
        var key = SandboxKeyComposer.ComposeForGroup(repoCount, repo.Name, repoGroupCount, langSlug, resourceSlug);
        // p0268: a key clash now means two GENUINELY different groups composed the same
        // name (e.g. same lang + same size token, different image). Disambiguate LOUDLY
        // rather than silently dropping the second (feedback_no_silent_defer).
        key = EnsureUniqueKey(key);
        // p0249: record the owning repo for this key the moment it is composed —
        // authoritative, so SandboxesForRepo never has to parse it back out.
        _sandboxRepos[key] = repo.Name;
        _groupKeyToSandboxKey[groupIdentity] = key;
        var sandbox = await CreateOneAsync(representative, spec, discoveriesInGroup, key, context, ct);
        _sandboxes[key] = sandbox;
        _discoveries[key] = representative;
        _contextsBySandbox[key] = discoveriesInGroup.ToList();
        StartLivenessWatcher(key, sandbox);
        await PublishCreatedAsync(key, representative, projectConfig, ct);
    }

    private void MergeContexts(string key, IReadOnlyList<RemoteContextDiscovery> discoveriesInGroup)
    {
        foreach (var d in discoveriesInGroup)
            if (!_contextsBySandbox[key].Any(existing =>
                    string.Equals(existing.ContextName, d.ContextName, StringComparison.Ordinal)))
                _contextsBySandbox[key].Add(d);
    }

    private void StartLivenessWatcher(string sandboxKey, ISandbox sandbox)
    {
        if (string.IsNullOrEmpty(_runId)) return;
        livenessSupervisor.Watch(_runId!, sandboxKey, sandbox);
    }

    private static string LangSlug(string? language) =>
        string.IsNullOrEmpty(language) ? "generic" : language.ToLowerInvariant().Replace(' ', '-');

    // p0268: the group identity is (toolchain image, resolved resources). Newline-
    // separated so neither part can spoof the other (image strings contain ':' and '/').
    private static string GroupKey(SandboxSpec spec) =>
        $"{spec.ToolchainImage}\n{ResourceSlug(spec.Resources)}";

    // p0268: a short, operator-readable size token (cpu_limit + memory_limit, the two
    // operationally meaningful caps) used both for the group key and to disambiguate
    // same-lang sandbox keys, e.g. "2-4gi" vs "500m-512mi".
    private static string ResourceSlug(ResourceLimits r) =>
        Sanitize($"{r.CpuLimit}-{r.MemoryLimit}");

    private static string Sanitize(string raw) =>
        new(raw.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    // p0268: guarantee the composed key is unique within this run. The resource slug
    // covers same-image-different-size; this numeric backstop covers the residual
    // (same lang + same size token + different image) so a distinct sandbox is never
    // silently merged away. It WARNs because needing it means the slug under-distinguished.
    private string EnsureUniqueKey(string key)
    {
        if (!_sandboxes.ContainsKey(key)) return key;
        for (var n = 2; ; n++)
        {
            var candidate = $"{key}-{n}";
            if (!_sandboxes.ContainsKey(candidate))
            {
                logger.LogWarning(
                    "Sandbox key '{Key}' already in use; using '{Candidate}' so the distinct sandbox is not dropped.",
                    key, candidate);
                return candidate;
            }
        }
    }

    private Task PublishCreatedAsync(
        string sandboxKey, RemoteContextDiscovery discovery, ResolvedProject projectConfig, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_runId)) return Task.CompletedTask;
        var image = sandboxSpecBuilder.Build(
            projectConfig, discovery.Language, _pipelineName, discovery.ToolchainImage, discovery.Resources).ToolchainImage;
        return eventPublisher.PublishAsync(
            new SandboxCreatedEvent(_runId!, sandboxKey, image, discovery.Language, DateTimeOffset.UtcNow), ct);
    }

    private async Task<ISandbox> CreateOneAsync(
        RemoteContextDiscovery representative, SandboxSpec spec,
        IReadOnlyList<RemoteContextDiscovery> discoveriesInGroup,
        string key, PipelineContext context, CancellationToken ct)
    {
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
        _groupKeyToSandboxKey.Clear();
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
