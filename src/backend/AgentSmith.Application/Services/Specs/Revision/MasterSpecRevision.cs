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
/// </summary>
public sealed class MasterSpecRevision(SpecRevisionGate gate, SpecRevisionText texts, SpecRevisionWriter writer)
{
    public async Task<string> ReviseAsync(PipelineContext pipeline, string? yaml, string? reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (string.IsNullOrWhiteSpace(reason)) return SpecRevisionReplies.NotRevised("give the reason for the revision");
        if (await gate.RefusalAsync(pipeline, ct) is { } refused) return SpecRevisionReplies.NotRevised(refused);
        var current = pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec);
        var (draft, refusal) = texts.Read(current, yaml, pipeline.Get<SpecSet>(ContextKeys.SpecSet).IsStandIn(current.SpecId));
        return draft is null
            ? SpecRevisionReplies.NotRevised(refusal!)
            : await writer.WriteAsync(pipeline, draft, reason, ct);
    }
}
