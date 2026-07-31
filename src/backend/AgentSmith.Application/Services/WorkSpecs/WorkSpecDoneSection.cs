using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: ONE source of criteria, and it is p0328's ratified expectation — never
/// a second list invented by the derivation. While an expectation exists the
/// spec's done-section CARRIES its assertions verbatim and is READ-ONLY, because
/// the verdict still pairs against the expectation: a master free to revise the
/// criteria it works toward would be working to a target nobody scores.
/// NegotiateExpectation is unconditional in FixBug and AddFeature, so this is the
/// normal case; FixNoTest has no expectation and there the spec's own list is the
/// only one and is revisable like the rest.
/// </summary>
public static class WorkSpecDoneSection
{
    /// <summary>The prompt instruction for the derivation's done-criteria.</summary>
    public static string Instruction(PipelineContext pipeline) =>
        TryGetRatified(pipeline, out _)
            ? "the done-criteria are supplied by the ratified acceptance contract and are "
              + "overwritten after you answer. Leave \"done\" empty; do not invent a second list."
            : "state the observable conditions under which this work is finished. Each entry is "
              + "checkable as true or false after the change. This run negotiated no separate "
              + "acceptance contract, so this list is the only one.";

    /// <summary>
    /// Applies the one-list rule to a derived spec: the ratified assertions verbatim
    /// and read-only when an expectation exists, the model's own list otherwise.
    /// </summary>
    public static WorkSpec Apply(WorkSpec spec, PipelineContext pipeline) =>
        TryGetRatified(pipeline, out var expectation)
            ? spec with { Done = expectation!.Draft.Expected, DoneIsReadOnly = true }
            : spec with { DoneIsReadOnly = false };

    private static bool TryGetRatified(PipelineContext pipeline, out RatifiedExpectation? expectation)
    {
        expectation = pipeline.TryGet<RatifiedExpectation>(ContextKeys.RunExpectation, out var e)
            ? e : null;
        return expectation is not null && expectation.Draft.Expected.Count > 0;
    }
}
