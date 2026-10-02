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
    ILogger logger) : ISourceScopeSandbox
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ISandbox? _inner;

    public string RepoName => address;

    /// <summary>2026-10-01-283de: the set behind the address, which render_reference copies into its browser.</summary>
    public string SetId => setId;
    public bool IsMaterialized => _inner is not null;

    /// <summary>The set's content hash once it is in the sandbox.</summary>
    public string? ResolvedSha { get; private set; }
    public string JobId => _inner?.JobId ?? $"reference-{setId}";

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        if (Refusal(step) is { } refused) return refused;
        try
        {
            var inner = await EnsureAsync(cancellationToken);
            return await inner.RunStepAsync(step, progress, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Reference '{Address}' failed to materialise", address);
            return SourceScopeRefusal.Because(step, $"The upload '{address}' could not be opened: {ex.Message}");
        }
    }

    private static StepResult? Refusal(Step step) => step.Kind switch
    {
        StepKind.ReadFile or StepKind.ListFiles or StepKind.Grep or StepKind.DirectoryTree or StepKind.Run => null,
        _ => SourceScopeRefusal.Because(step,
            $"Step kind '{step.Kind}' is not served on an upload's container — it serves the file reads and "
            + "commands (run_in_reference); a command may write into the copy."),
    };

    public async Task<string> MaterializeAsync(CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        return ResolvedSha ?? string.Empty;
    }

    private async Task<ISandbox> EnsureAsync(CancellationToken ct)
    {
        if (_inner is not null) return _inner;
        await _gate.WaitAsync(ct);
        try
        {
            if (_inner is not null) return _inner;
            logger.LogInformation("Materialising reference '{Address}' (set {SetId})", address, setId);
            (var opened, ResolvedSha) = await hold.OpenAsync(
                (conversation, c) => opener.SpawnAsync(project, c, conversation, withoutSecrets: true),
                (sandbox, c) => materialiser.PrepareAsync(sandbox, conversationId, setId, c), ct);
            _inner = opened;
            return opened;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        var inner = _inner;
        _inner = null;
        // A held inner sandbox goes back to the register: the next turn reads through it.
        if (inner is not null && !hold.Keep(inner)) await inner.DisposeAsync();
        _gate.Dispose();
    }
}
