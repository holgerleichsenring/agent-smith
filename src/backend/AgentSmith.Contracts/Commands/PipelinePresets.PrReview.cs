namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0167a: agent-smith PERFORMS a PR review (distinct from the PR-feedback
    // loop where it responds to review comments). Triggered by pr-opened /
    // pr-synchronize webhooks; the trigger seeds PrNumber + CheckoutBranch
    // (PR head) + SourceOverrideRepo (the PR's repo) into the initial context.
    //
    // p0312c took the p0179d collapse this preset had been exempted from. It was
    // the last holdout because it was the only pipeline without a master, and the
    // Triage selector existed to choose reviewers for it. One pr-review-master now
    // covers all four dimensions and decides its own fan-out via spawn_agents, so
    // LoadSkills + Triage + RunReviewPhase give way to AgenticMaster +
    // MergeMasterFindings — the same shape security-scan has run since p0179d.
    // CompilePrReviewFindings already reads ContextKeys.SkillObservations and is
    // untouched.
    //
    // The preset could never have run before p0312c: PipelineNameInitializer
    // publishes the name through SetEnum, which throws on an undeclared value, and
    // "pr-review" was missing from the catalog's pipeline_name enum.
    //
    // Shape notes against the original p0167 spec:
    // - FetchPullRequest was folded into AnalyzePrDiff — the handler reads the
    //   PR head/base + per-file patches from IPrDiffProvider itself.
    // - AnalyzeProject == CommandNames.AnalyzeCode (AnalyzeProjectHandler).
    // - WriteRunResult runs BEFORE PostPrComments (write-then-deliver, like
    //   fix-bug) so the summary comment can link to an existing result.md.
    public static readonly IReadOnlyList<string> PrReview =
    [
        CommandNames.LoadCatalog,
        CommandNames.PipelineNameInitializer,
        CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate,
        CommandNames.LoadCodingPrinciples, CommandNames.LoadMemoryIndex, // p0380
        CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        CommandNames.AnalyzePrDiff,
        CommandNames.AgenticMaster,        // p0312c: resolves pr-review-master
        CommandNames.MergeMasterFindings,  // master observations -> SkillObservations
        CommandNames.CompilePrReviewFindings,
        CommandNames.WriteRunResult,
        CommandNames.PostPrComments,
    ];
}
