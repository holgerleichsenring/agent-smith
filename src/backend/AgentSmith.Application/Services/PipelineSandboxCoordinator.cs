using AgentSmith.Application.Models;
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
/// The run's sandboxes (p0158e, p0161a). 2026-11-25-9992b: a run has ONE workspace — one pod or volume set with a
/// container per toolchain image, every repository side by side under /work — and each (repository, image) group of
/// contexts is a sandbox key served by its image's container. A group whose image the workspace lacks (a repository
/// brought in mid-run) restarts the workspace with that container (<see cref="WorkspaceRestart"/>).
/// The Sandboxes dictionary is keyed by SandboxKeyComposer's composite key; SandboxDiscoveries carries each key's
/// Workdir + Language.
///
/// Lifetime: transient / per-pipeline-run. Owns mutable state, so the DI registration MUST be transient.
/// </summary>
public sealed class PipelineSandboxCoordinator(
    IWorkspaceFactory workspaces,
    WorkspaceGroupPlanner planner,
    WorkspaceRestart restart,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    ISandboxLivenessSupervisor livenessSupervisor,
    SandboxRelease release,
    ILogger<PipelineSandboxCoordinator> logger) : IPipelineSandboxCoordinator
{
    private readonly Dictionary<string, ISandbox> _sandboxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RemoteContextDiscovery> _discoveries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<RemoteContextDiscovery>> _contextsBySandbox = new(StringComparer.Ordinal);
    // p0249: sandbox key -> owning repo name, recorded where the key is composed.
    private readonly Dictionary<string, string> _sandboxRepos = new(StringComparer.Ordinal);
    // 2026-08-31-7097: sandbox key -> the toolchain image the BACKEND pulled, read from its container.
    private readonly Dictionary<string, string> _sandboxImages = new(StringComparer.Ordinal);
    // p0268: group identity (repo, image) -> sandbox key, so a repeated call reuses the key.
    private readonly Dictionary<string, string> _groupKeyToSandboxKey = new(StringComparer.Ordinal);
    private readonly List<WorkspaceGroup> _groups = [];
    // 2026-11-25-9992b: one container reserves once; its first key's SandboxCreated carries the request.
    private readonly HashSet<string> _announcedImages = new(StringComparer.Ordinal);
    private RunWorkspace? _workspace;
    private string? _runId;
    // p0320a: read once from the PipelineContext so every spec build sizes pipeline-aware.
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
        var repos = context.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        var fresh = new List<WorkspaceGroup>();
        foreach (var group in await planner.PlanAsync(projectConfig, context, _pipelineName, repos, cancellationToken))
        {
            if (_groupKeyToSandboxKey.TryGetValue(group.Identity, out var cachedKey)) MergeContexts(cachedKey, group.Discoveries);
            else if (!fresh.Any(f => f.Identity == group.Identity)) fresh.Add(group);
        }
        if (fresh.Count > 0) await EnsureWorkspaceAsync(fresh, context, cancellationToken);
        foreach (var group in fresh) await AttachAsync(group, repos.Count, cancellationToken);
        Publish(context, projectConfig);
        return _sandboxes;
    }

    public async Task<IReadOnlyList<ResourceLimits>> AddedToolchainsAsync(
        ResolvedProject projectConfig, PipelineContext context, RepoConnection repo, CancellationToken cancellationToken)
    {
        var groups = await planner.PlanAsync(projectConfig, context, _pipelineName, [repo], cancellationToken);
        return [.. planner.Toolchains(groups)
            .Where(t => _workspace?.Serves(t.Image) != true).Select(t => t.Resources)];
    }

    private async Task EnsureWorkspaceAsync(
        IReadOnlyList<WorkspaceGroup> fresh, PipelineContext context, CancellationToken ct)
    {
        IReadOnlyList<WorkspaceGroup> all = [.. _groups, .. fresh];
        var toolchains = planner.Toolchains(all);
        _workspace ??= new RunWorkspace(workspaces);
        if (!_workspace.IsCreated)
            await _workspace.CreateAsync(SpecFor(toolchains, all, context), ct);
        else if (toolchains.Any(t => !_workspace.Serves(t.Image)))
            foreach (var note in await restart.RestartAsync(
                         context, _workspace, SpecFor(toolchains, all, context), livenessSupervisor, WatchAll, ct))
                logger.LogWarning("Workspace restart: {Note}", note);
        _groups.AddRange(fresh);
    }

    // The base is project-level (agent image, security, secrets, step cap); every group of the run shares it.
    private WorkspaceSpec SpecFor(
        IReadOnlyList<WorkspaceToolchain> toolchains, IReadOnlyList<WorkspaceGroup> groups, PipelineContext context)
    {
        var hostSource = context.TryGet<string>(ContextKeys.SourcePath, out var source) && !string.IsNullOrEmpty(source)
            ? source : null;
        var mounts = hostSource is null ? null : groups.Select(g => g.Repo.Name).Distinct(StringComparer.Ordinal)
            .ToDictionary(name => name, _ => hostSource, StringComparer.Ordinal);
        var first = toolchains[0];
        return new WorkspaceSpec(
            groups[0].Spec with { ToolchainImage = first.Image, Resources = first.Resources, RunId = _runId },
            toolchains, mounts);
    }

    private async Task AttachAsync(WorkspaceGroup group, int repoCount, CancellationToken ct)
    {
        var representative = group.Discoveries[0];
        // p0322b: multi-group keys carry the representative's CONTEXT NAME, unique per repo by construction.
        var key = EnsureUniqueKey(SandboxKeyComposer.ComposeForGroup(
            repoCount, group.Repo.Name, group.RepoGroupCount, representative.ContextName));
        _sandboxRepos[key] = group.Repo.Name;
        _groupKeyToSandboxKey[group.Identity] = key;
        var container = new WorkspaceContainerSandbox(_workspace!, group.Image);
        if (container.Backend() is ISandboxToolchainImage named && !string.IsNullOrEmpty(named.ToolchainImage))
            _sandboxImages[key] = named.ToolchainImage;
        // 2026-11-25-9992c: the repository's own root outermost, so every caller of this key speaks to /work/<repo>.
        var sandbox = new RepoSandbox(
            new SandboxEventProjector(container, eventPublisher, runContext, key, logger), group.Repo.Name);
        _sandboxes[key] = sandbox;
        _discoveries[key] = representative;
        _contextsBySandbox[key] = [.. group.Discoveries];
        StartLivenessWatcher(key, sandbox);
        logger.LogInformation("Sandbox {Key}: image={Image} in the run's workspace, contexts [{Contexts}]",
            key, group.Image, string.Join(", ", group.Discoveries.Select(d => $"{d.ContextName}@{d.Workdir}")));
        await PublishCreatedAsync(key, group, ct);
    }

    private void Publish(PipelineContext context, ResolvedProject projectConfig)
    {
        context.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, _sandboxes);
        context.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, _sandboxRepos);
        context.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries, _discoveries);
        context.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxImages, _sandboxImages);
        context.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            _contextsBySandbox.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<RemoteContextDiscovery>)kv.Value, StringComparer.Ordinal));
        context.Set(ContextKeys.Sandbox, _sandboxes[_sandboxes.Keys.First()]);
        // p0331: the LIVE coordinator + project, so ensure_repo_sandbox grows THIS run's workspace. The coordinator is
        // owned by PipelineExecutor (`await using`); a DI resolve inside a handler would yield a fresh empty instance.
        context.Set<IPipelineSandboxCoordinator>(ContextKeys.SandboxCoordinator, this);
        context.Set(ContextKeys.ProjectConfig, projectConfig);
    }

    private void MergeContexts(string key, IReadOnlyList<RemoteContextDiscovery> discoveriesInGroup)
    {
        foreach (var d in discoveriesInGroup)
            if (!_contextsBySandbox[key].Any(existing =>
                    string.Equals(existing.ContextName, d.ContextName, StringComparison.Ordinal)))
                _contextsBySandbox[key].Add(d);
    }

    private void WatchAll()
    {
        foreach (var (key, sandbox) in _sandboxes) StartLivenessWatcher(key, sandbox);
    }

    private void StartLivenessWatcher(string sandboxKey, ISandbox sandbox)
    {
        if (string.IsNullOrEmpty(_runId)) return;
        livenessSupervisor.Watch(_runId!, sandboxKey, sandbox);
    }

    // p0268: a key clash means two genuinely different groups composed the same name (sanitization collapsing two
    // context names); disambiguate LOUDLY rather than silently dropping the second.
    private string EnsureUniqueKey(string key)
    {
        if (!_sandboxes.ContainsKey(key)) return key;
        for (var n = 2; ; n++)
        {
            var candidate = $"{key}-{n}";
            if (_sandboxes.ContainsKey(candidate)) continue;
            logger.LogWarning(
                "Sandbox key '{Key}' already in use; using '{Candidate}' so the distinct sandbox is not dropped.",
                key, candidate);
            return candidate;
        }
    }

    // p0332: the resolved memory request, so reserved resource-time is the workspace's real reservation — carried by
    // the first key of each container only, because the keys of one container share its reservation.
    private Task PublishCreatedAsync(string sandboxKey, WorkspaceGroup group, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_runId)) return Task.CompletedTask;
        // A backend that serves any image (in-process) gains no container, so the group's own request stands in.
        var reserved = _announcedImages.Add(group.Image)
            ? (_workspace!.Spec!.Toolchains.FirstOrDefault(t => t.Image == group.Image)?.Resources ?? group.Spec.Resources).MemoryRequest
            : null;
        return eventPublisher.PublishAsync(
            new SandboxCreatedEvent(_runId!, sandboxKey, group.Image, group.Discoveries[0].Language,
                DateTimeOffset.UtcNow, MemoryRequest: reserved), ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        // p0201: stop watchers BEFORE the workspace goes, so a graceful shutdown never trips "vanished".
        await livenessSupervisor.DisposeAsync();
        // 2026-10-13-9a59i: each key's exit is read through its container before the workspace removes it.
        await release.ReleaseAllAsync(_runId, _sandboxes);
        if (_workspace is not null) await _workspace.DisposeAsync();
        _sandboxes.Clear();
        _discoveries.Clear();
        _contextsBySandbox.Clear();
        _groupKeyToSandboxKey.Clear();
    }
}
