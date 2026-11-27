using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: the coding master revises the spec it is building — what an operator's message or a corrected
/// premise asks for — instead of pausing the run. 2026-11-19-3e10: only mechanical checks stand between the text and
/// the branch: a reason, the gate (<see cref="SpecRevisionGate"/>), the schema and identity read
/// (<see cref="SpecRevisionText"/>); then the writer records it and its criteria become the spec's
/// (<see cref="SpecRevisionWriter"/>). No reviewer judges the master's text; it may brief one itself.
/// 2026-11-27-6625: until the spec is done it may move to another repository, which joins the workspace first
/// (<see cref="SpecRepoJoin"/>).
/// 2026-11-27-3532d: with <c>retire</c> the spec cannot be implemented as written: it is retired and the text is its
/// replacement (<see cref="SpecRetirement"/>), written as one revision.
/// </summary>
public sealed class MasterSpecRevision(
    SpecRevisionGate gate, SpecRevisionText texts, SpecRepoJoin join, SpecRevisionWriter writer, SpecRetirement retirement)
{
    /// <param name="ensure">2026-11-27-6625: the master's ensure_repo_sandbox, for a repository the revision moves to.</param>
    /// <param name="retire">2026-11-27-3532d: retire the spec and write <paramref name="yaml"/> as its replacement.</param>
    public async Task<string> ReviseAsync(
        PipelineContext pipeline, string? yaml, string? reason, CancellationToken ct,
        Func<string, CancellationToken, Task<string>>? ensure = null, bool retire = false)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (string.IsNullOrWhiteSpace(reason)) return SpecRevisionReplies.NotRevised("give the reason for the revision");
        if (await gate.RefusalAsync(pipeline, ct) is { } refused) return SpecRevisionReplies.NotRevised(refused);
        var current = pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec);
        if (retire) return await RetireAsync(pipeline, current, yaml, reason, ensure, ct);
        var set = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        var (draft, refusal) = texts.Read(current, yaml, set.IsStandIn(current.SpecId),
            mayMoveRepos: !set.Executed.Contains(current.SpecId, StringComparer.Ordinal));
        if (draft is null) return SpecRevisionReplies.NotRevised(refusal!);
        return await join.RefusalAsync(pipeline, current, draft, ensure, ct) is { } absent
            ? SpecRevisionReplies.NotRevised(absent)
            : await writer.WriteAsync(pipeline, draft, reason, ct);
    }

    private async Task<string> RetireAsync(PipelineContext pipeline, SpecDraft current, string? yaml, string reason,
        Func<string, CancellationToken, Task<string>>? ensure, CancellationToken ct)
    {
        var (set, replacement, refusal) = retirement.Retire(pipeline, current, yaml);
        if (set is null || replacement is null) return SpecRevisionReplies.NotRevised(refusal!);
        return await join.RefusalAsync(pipeline, current, replacement, ensure, ct) is { } absent
            ? SpecRevisionReplies.NotRevised(absent)
            : await writer.RetireAsync(pipeline, set, replacement, current.SpecId, reason, ct);
    }
}
