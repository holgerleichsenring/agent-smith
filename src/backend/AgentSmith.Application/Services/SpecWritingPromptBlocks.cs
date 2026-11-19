using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-19-6b0ba: what the coding master is told when the spec it is on is not yet written — the ticket's
/// stand-in — or when an input arrived over the spec it is on, and the write-only contract of a spec derivation. The
/// master writes the spec itself, the way a spec-first session does; no other model cut it.
/// </summary>
public static class SpecWritingPromptBlocks
{
    /// <summary>The stand-in block, with the epic and the question pin it is read against; empty for a written spec.</summary>
    public static string StandIn(PipelineContext pipeline, SpecDraft draft)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        if (!IsStandIn(pipeline, draft)) return string.Empty;
        var repo = draft.Repos is [var only] ? $" for {only}" : string.Empty;
        var pin = pipeline.TryGet<string>(ContextKeys.SpecQuestionPin, out var p) && !string.IsNullOrWhiteSpace(p) ? $"\n{p}\n" : string.Empty;
        return $"""

            ## Write the spec first
            No spec was written for this ticket: the spec above is the ticket as it came in, standing in for the spec
            {draft.SpecId}{repo}. Before anything else, read the ticket and the code it touches and write the spec — a
            one-sentence goal, the steps, and done lines a diff or a command can show — and record it with revise_spec
            (same spec id, requires and repos; you may add applies_to). Then work from what you wrote. When this
            repository has nothing to do for the ticket, write the spec to say so (done: no change in it) and finish.
            Work beyond one spec is a follow-up you propose in finish_spec.
            {pin}{EpicGroundPromptSection.Build(pipeline)}
            """;
    }

    /// <summary>The input that arrived over the spec this run kept; empty when none did.</summary>
    public static string KeptInput(PipelineContext pipeline, SpecDraft draft)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (IsStandIn(pipeline, draft) || !pipeline.TryGet<string>(ContextKeys.SpecKeptInput, out var cause)
            || string.IsNullOrWhiteSpace(cause))
            return string.Empty;
        return $"\nThis run started on new input ({cause}) over the spec as it stands. Read it in the ticket and its "
            + "conversation; when it changes what this spec must make true, amend the spec with revise_spec first.\n";
    }

    /// <summary>True when the run is a spec derivation: the master writes the spec and builds nothing.</summary>
    public static bool IsWriteOnly(PipelineContext pipeline) => PipelinePresets.LeavesTicketAlone(pipeline);

    /// <summary>The contract of a spec derivation, in place of the build contract.</summary>
    public static string WriteOnlyContract(SpecDraft draft) => $"""

        ## Spec contract
        This run writes SPEC {draft.SpecId} and builds nothing: a person reviews the spec before any code is written.
        Read the ticket and the code until the spec's premises hold, write the spec as above and record it with
        revise_spec. Change no code and commit nothing. When only a person can answer what the spec must say, call
        finish_spec with status blocked and the question as the blocker; it is posted on the ticket. Otherwise report
        what you wrote and why, and call finish_spec with status no-tests.
        """;

    private static bool IsStandIn(PipelineContext pipeline, SpecDraft draft) =>
        pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) && set is not null && set.IsStandIn(draft.SpecId);
}
