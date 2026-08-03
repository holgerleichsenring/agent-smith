namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>p0393: the one code-changing pipeline. Trigger routing, the CLI and
    /// the AgenticMaster's pipeline-name branch all key off this name.</summary>
    public const string CodeName = "code";

    // p0393: fix-bug, fix-no-test, add-feature and phase-execution collapse into this
    // one preset. They differed only in steps the phase spec now carries:
    //   - add-feature's GenerateTests/GenerateDocs are spec steps when the work needs them
    //   - fix-bug's reproduction grounding is a step in the plan
    //   - fix-bug's "no code changes = FAIL" is an unmet done-criterion at VerifyPhase
    //   - fix-no-test was fix-bug minus NegotiateExpectation, which this phase deletes
    // The ticket label (bug / feature / phase) therefore stops selecting a pipeline and
    // becomes an input to the spec instead.
    //
    // Corrections this shape carries against its predecessors:
    //
    // - PhaseSpecGate replaces GeneratePlan's old input, NOT GeneratePlan. p0315d could
    //   publish the spec AS the approved plan because its tickets were authored by the
    //   operator in-thread, where the planning had already happened. A spec states WHAT is
    //   expected; with no code samples there is no plan in it. GeneratePlan stays and its
    //   input changes: the validated spec plus the codebase, which is the step a human
    //   developer performs before writing a line.
    //
    // - NegotiateExpectation and Approval are gone. The spec IS the negotiated expectation,
    //   now explicit, schema-validated and revisable, so a separate negotiation restates the
    //   same intent a third time; Approval blocked the run on an operator who is not there.
    //   PlanOpenQuestions stays, because planning against the code is where "the requirement
    //   does not match what is in the repository" becomes visible.
    //
    // - VerifyPhase is the point. p0216 moved build+test to the coding master as a
    //   RESPONSIBILITY and left nothing that refuses the PR when the build is red — green
    //   was a model claim with no second opinion.
    public static readonly IReadOnlyList<string> Code =
    [
        CommandNames.LoadCatalog,
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket,
        CommandNames.ScopeRepos, // p0331: narrow to ticket-affected repos before any sandbox
        CommandNames.CheckoutSource,
        CommandNames.SetupRegistryAuth, // p0198: pre-stage private-feed credentials
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a strict gate
        CommandNames.LoadCodingPrinciples, CommandNames.LoadMemoryIndex, // p0380
        CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        // p0393a: any ticket becomes an ordered SET of phase specs here — after
        // AnalyzeCode, because "the requirement contradicts the repository" is only
        // findable once the code has been read.
        CommandNames.DeriveSpec,
        CommandNames.SpecHandback,        // not implementable / contradicts the repo => park
        CommandNames.PhaseSpecGate,       // spec validated before a single master token
        CommandNames.EnsurePrerequisites, // p0202e: analyzer-derived, before the master
        // p0393a: one plan → master → verify → record block per derived phase, spliced in
        // order. p0328's NegotiateExpectation is gone with it: every run now carries a
        // schema-validated done-list, so negotiating a second acceptance contract would
        // restate the same intent a third time.
        CommandNames.PhaseSequence,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
