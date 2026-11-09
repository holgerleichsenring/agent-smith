using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-01-283dc: one uploaded website as a READ-ONLY address in a design turn's sandbox map,
/// so read_file, grep and directory_tree see its HTML and CSS the way they see a repository.
/// <para>
/// Nothing spawns until the first step is served. It needs no repository: the container comes
/// from <see cref="SourceScopeOpener.SpawnAsync"/> on the generic image and is filled by
/// <see cref="ReferenceSetMaterialiser"/>. Steps go through <see cref="SourceScopeRefusal"/>
/// with NO writable prefix, and the inner sandbox is held for the conversation under the set's
/// id — immutable content, so a held one is never rewritten.
/// </para>
/// <para>
/// 2026-10-02-075dc: THE COPY IS THE SCOPE. The container holds the upload and nothing else, so it
/// serves every Run step — run_in_reference's shell included — besides the four reads; a write
/// step stays refused (a command writes into the copy, never into the store). It is spawned
/// without the project's sandbox secrets: a copy of an upload needs none.
/// </para>
/// </summary>
public sealed class ReferenceSetSandbox(
    ResolvedProject project,
    string conversationId,
    string address,
    string setId,
    SourceScopeHold hold,
    SourceScopeOpener opener,
    ReferenceSetMaterialiser materialiser,
    ILogger logger,
    SandboxStepWatch? watch = null,
    ISourceScopeObserverAccessor? observers = null) : ISourceScopeSandbox
{
    /// <summary>2026-11-09-9631a: the cached sandbox, and what happens when it turns out to be gone.</summary>
    private readonly ReopeningStepRunner _steps = new(address, watch, logger);
    private readonly ReferenceReadLog _reads = new();

    public string RepoName => address;

    /// <summary>2026-10-01-283de: the set behind the address, which render_reference copies into its browser.</summary>
    public string SetId => setId;

    /// <summary>2026-10-02-075dd: the conversation the set was uploaded to — where its note is kept.</summary>
    public string ConversationId => conversationId;

    /// <summary>2026-10-02-075dd: the set's note as the turn began, or null.</summary>
    public string? Note { get; init; }

    /// <summary>2026-11-04-96a0c: what earlier turns opened of the set, read as the turn began, or null.</summary>
    public ReferenceCoverage? Coverage { get; init; }

    /// <summary>2026-11-04-96a0c: the set-relative paths read_file opened through this sandbox in this turn.</summary>
    public IReadOnlyCollection<string> OpenedPaths => _reads.Paths;
    public bool IsMaterialized => _steps.Slot.Current is not null || _steps.Slot.Unavailable is not null;

    /// <summary>The set's content hash once it is in the sandbox.</summary>
    public string? ResolvedSha { get; private set; }
    public string JobId => _steps.Slot.Current?.JobId ?? $"reference-{setId}";

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        if (Refusal(step) is { } refused) return refused;
        return _reads.Record(step, await _steps.RunAsync(step, progress, OpenAsync,
            ex => $"The upload '{address}' could not be opened: {ex.Message}", NotRepeated, ReportAsync, cancellationToken));
    }

    // 2026-11-09-9631c: an upload draws no line for its first open, only when it has to reopen.
    private Task ReportAsync(SourceScopeProgress state, CancellationToken ct) =>
        observers?.Current?.ReportAsync(address, state, ct) ?? Task.CompletedTask;

    // 2026-11-09-9631a: a command may rest on what an earlier one installed or started in the lost
    // container, so repeating it could fail for a reason nobody can see. The reads are safe to repeat.
    private string? NotRepeated(Step step) => step.Kind == StepKind.Run
        ? $"The container of the upload '{address}' was removed and a fresh one opened, so this command did "
          + "not finish and what earlier commands installed or started is gone. Set it up again, then rerun it."
        : null;

    private static StepResult? Refusal(Step step) => step.Kind switch
    {
        StepKind.ReadFile or StepKind.ListFiles or StepKind.Grep or StepKind.DirectoryTree or StepKind.Run => null,
        _ => SourceScopeRefusal.Because(step,
            $"Step kind '{step.Kind}' is not served on an upload's container — it serves the file reads and "
            + "commands (run_in_reference); a command may write into the copy."),
    };

    public async Task<string> MaterializeAsync(CancellationToken cancellationToken)
    {
        await _steps.Slot.EnsureAsync(OpenAsync, cancellationToken);
        return ResolvedSha ?? string.Empty;
    }

    private async Task<ISandbox> OpenAsync(CancellationToken ct)
    {
        logger.LogInformation("Materialising reference '{Address}' (set {SetId})", address, setId);
        (var opened, ResolvedSha) = await hold.OpenAsync(
            (conversation, c) => opener.SpawnAsync(project, c, conversation, withoutSecrets: true),
            (sandbox, c) => materialiser.PrepareAsync(sandbox, conversationId, setId, c), ct);
        return opened;
    }

    public async ValueTask DisposeAsync()
    {
        var inner = _steps.Slot.Take();
        // A held inner sandbox goes back to the register: the next turn reads through it.
        if (inner is not null && !hold.Keep(inner)) await inner.DisposeAsync();
        _steps.Dispose();
    }
}
