using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0315b: lazy READ-ONLY sandbox over one repo of a spec-dialog scope. Nothing spawns
/// until the first step is served. Owner disposes per turn; the agent's idle self-exit and
/// the orphan reaper are the backstops.
/// <para>2026-09-22-46ef: read-only is a rule about DAMAGE, not about step kinds — the
/// reads, the program allowance and the write policy live in
/// <see cref="SourceScopeRefusal"/>.</para>
/// <para>2026-09-13-9802: a scope may name a revision. The git ladder that lands on it, and
/// the five refusals it tells apart, live in <see cref="SourceScopeMaterialiser"/> — which
/// also issues the clone, before this guard applies; <see cref="SourceScopeOpener"/> spawns
/// it. Here: the guard, the lifetime, and the progress an ambient observer is told.</para>
/// </summary>
public sealed class SourceScopeSandbox(
    ResolvedProject project,
    RepoConnection repo,
    string? revision,
    SourceScopeOpener opener,
    ISourceScopeObserverAccessor observers,
    ILogger logger,
    IReadOnlyCollection<string>? writablePrefixes = null) : ISourceScopeSandbox
{
    private readonly SemaphoreSlim _materializeGate = new(1, 1);

    /// <summary>2026-09-22-46ef: empty means every write is refused — the default.</summary>
    private readonly SourceScopeWritePolicy _writes = new(writablePrefixes ?? []);

    private ISandbox? _inner;

    public string RepoName => repo.Name;
    public bool IsMaterialized => _inner is not null;
    public string? ResolvedSha { get; private set; }
    public string JobId => _inner?.JobId ?? $"source-scope-{repo.Name}";

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        if (SourceScopeRefusal.Unless(step, _writes) is { } refused) return refused;
        if (string.IsNullOrEmpty(repo.Url))
            return SourceScopeRefusal.Because(step, NoCloneUrl().Message);

        try
        {
            var inner = await EnsureMaterializedAsync(cancellationToken);
            return await inner.RunStepAsync(step, progress, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Source scope '{Repo}' failed to materialise", repo.Name);
            return SourceScopeRefusal.Because(step, ex.Message);
        }
    }

    /// <summary>2026-09-13-9802: the preparation a read triggers, made explicit so a caller
    /// that must REFUSE learns the KIND before it spends anything — the typed failure escapes
    /// here instead of becoming a step-refusal sentence.</summary>
    public async Task<string> MaterializeAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(repo.Url)) throw NoCloneUrl();
        await EnsureMaterializedAsync(cancellationToken);
        return ResolvedSha ?? string.Empty;
    }

    private SourceScopeUnavailableException NoCloneUrl() => new(
        SourceScopeFailureKind.NoCloneUrl, repo.Name, revision,
        $"Repo '{repo.Name}' has no clone URL configured — source grounding is "
        + "unavailable for it; answer from the code map or say what is missing.");

    private async Task<ISandbox> EnsureMaterializedAsync(CancellationToken ct)
    {
        if (_inner is not null) return _inner;
        await _materializeGate.WaitAsync(ct);
        try
        {
            if (_inner is not null) return _inner;
            logger.LogInformation(
                "Materialising source scope '{Repo}' at '{Revision}'",
                repo.Name, revision ?? "the clone's own default");
            await ReportAsync(SourceScopeProgress.Opening, ct);
            try
            {
                (var opened, ResolvedSha) = await opener.OpenAsync(project, repo, revision, ct);
                _inner = opened;
            }
            catch
            {
                // Not open, so the next read reports opening again; this ends the line it drew.
                await ReportAsync(SourceScopeProgress.Failed, CancellationToken.None);
                throw;
            }
            await ReportAsync(SourceScopeProgress.Ready, ct);
            return _inner;
        }
        finally
        {
            _materializeGate.Release();
        }
    }

    // No observer set is the default: nothing is told, and the scope behaves as it always did.
    // The revision is part of the name told, because two templates may pin one repository twice.
    private Task ReportAsync(SourceScopeProgress progress, CancellationToken ct) =>
        observers.Current?.ReportAsync(
            revision is null ? repo.Name : $"{repo.Name}@{revision}", progress, ct)
        ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        var inner = _inner;
        _inner = null;
        if (inner is not null) await inner.DisposeAsync();
        _materializeGate.Dispose();
    }
}
