using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0315b: lazy READ-ONLY sandbox over one repo of a spec-dialog scope. Nothing spawns until
/// the first step is served. Here: the guard, the lifetime, and the progress an ambient
/// observer is told.
/// <para>2026-09-22-46ef: read-only is a rule about DAMAGE, not about step kinds — the reads,
/// the program allowance and the write policy live in <see cref="SourceScopeRefusal"/>.
/// 2026-09-13-9802: a scope may name a revision, and the git ladder that lands on it lives in
/// <see cref="SourceScopeMaterialiser"/>; <see cref="SourceScopeOpener"/> spawns what it runs in.</para>
/// <para>2026-09-22-2d11b: a scope built for a design conversation carries a
/// <see cref="SourceScopeHold"/>, and then the inner sandbox — never this object — outlives the
/// turn: this one caches its inner sandbox before it reports anything, so a second turn through
/// it would refresh nothing and report a stale sha.</para>
/// </summary>
public sealed class SourceScopeSandbox(
    ResolvedProject project,
    RepoConnection repo,
    string? revision, SourceScopeHold? hold,
    SourceScopeOpener opener,
    ISourceScopeObserverAccessor observers,
    ILogger logger,
    IReadOnlyCollection<string>? writablePrefixes = null,
    SandboxStepWatch? watch = null) : ISourceScopeSandbox
{
    /// <summary>2026-11-09-9631a: the cached sandbox, and what happens when it turns out to be gone.</summary>
    private readonly ReopeningStepRunner _steps = new(repo.Name, watch, logger);

    /// <summary>2026-09-22-46ef: empty means every write is refused — the default.</summary>
    private readonly SourceScopeWritePolicy _writes = new(writablePrefixes ?? []);

    public string RepoName => repo.Name;

    /// <summary>2026-11-09-9631a: a scope whose reopen failed was still opened: the record keeps its sha,
    /// and an evidence read reaches the refusal and mints a look that could not run.</summary>
    public bool IsMaterialized => _steps.Slot.Current is not null || _steps.Slot.Unavailable is not null;
    public string? ResolvedSha { get; private set; }
    public string JobId => _steps.Slot.Current?.JobId ?? $"source-scope-{repo.Name}";

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        if (SourceScopeRefusal.Unless(step, _writes) is { } refused) return Task.FromResult(refused);
        if (string.IsNullOrEmpty(repo.Url))
            return Task.FromResult(SourceScopeRefusal.Because(step, NoCloneUrl().Message));
        // A scope's steps are reads and programs the server built, so all of them may be repeated.
        return _steps.RunAsync(step, progress, OpenAsync, ex => ex.Message, _ => null, ReportAsync, cancellationToken);
    }

    /// <summary>2026-09-13-9802: the preparation a read triggers, made explicit so a caller that
    /// must REFUSE learns the KIND before it spends anything — the typed failure escapes here.</summary>
    public async Task<string> MaterializeAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(repo.Url)) throw NoCloneUrl();
        await _steps.Slot.EnsureAsync(OpenAsync, cancellationToken);
        return ResolvedSha ?? string.Empty;
    }

    private SourceScopeUnavailableException NoCloneUrl() => new(
        SourceScopeFailureKind.NoCloneUrl, repo.Name, revision,
        $"Repo '{repo.Name}' has no clone URL configured — source grounding is "
        + "unavailable for it; answer from the code map or say what is missing.");

    private async Task<ISandbox> OpenAsync(CancellationToken ct)
    {
        logger.LogInformation("Materialising source scope '{Repo}' at '{Revision}'",
            repo.Name, revision ?? "the clone's own default");
        // 2026-11-09-9631c: a reopen has its own line, drawn and ended by the runner.
        var told = !_steps.Slot.Reopened;
        if (told) await ReportAsync(SourceScopeProgress.Opening, ct);
        ISandbox opened;
        try
        {
            (opened, ResolvedSha) = hold is null
                ? await opener.OpenAsync(project, repo, revision, ct)
                : await hold.OpenAsync((conversation, c) => opener.SpawnAsync(project, c, conversation),
                    (sandbox, c) => opener.PrepareAsync(sandbox, repo, revision, c), ct);
        }
        catch
        {
            // Not open, so the next read reports opening again; this ends the line it drew.
            if (told) await ReportAsync(SourceScopeProgress.Failed, CancellationToken.None);
            throw;
        }
        if (told) await ReportAsync(SourceScopeProgress.Ready, ct);
        return opened;
    }

    // No observer set is the default: nothing is told. The revision is part of the name told,
    // because two templates may pin one repository twice.
    private Task ReportAsync(SourceScopeProgress progress, CancellationToken ct) =>
        observers.Current?.ReportAsync(revision is null ? repo.Name : $"{repo.Name}@{revision}", progress, ct)
        ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        var inner = _steps.Slot.Take();
        // A held inner sandbox goes back to the register: the next turn reads through it.
        if (inner is not null && hold?.Keep(inner) != true) await inner.DisposeAsync();
        _steps.Dispose();
    }
}
