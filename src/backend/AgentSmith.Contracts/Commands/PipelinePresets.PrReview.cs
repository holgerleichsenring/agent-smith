namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0167a: agent-smith PERFORMS a PR review (distinct from the PR-feedback
    // loop where it responds to review comments). Triggered by pr-opened /
    // pr-synchronize webhooks; the trigger seeds PrNumber + CheckoutBranch
    // (PR head) + SourceOverrideRepo (the PR's repo) into the initial context.
    //
    // Shape notes against the original p0167 spec:
    // - FetchPullRequest was folded into AnalyzePrDiff — the handler reads the
    //   PR head/base + per-file patches from IPrDiffProvider itself; a separate
    //   fetch step would only shuttle the same provider result through context.
    // - BootstrapProject was retired in p0131b; BootstrapCheck + BootstrapGate
    //   are the current equivalent (fail-fast on missing bootstrap files).
    // - AnalyzeProject == CommandNames.AnalyzeCode (AnalyzeProjectHandler).
    // - LoadSkills precedes Triage (StructuredTriageStrategy needs the roster).
    // - This preset keeps the phase-based Triage → RunReviewPhase chain (NOT the
    //   p0179 AgenticMaster collapse): the pr-review skills (p0167b) are
    //   observation-emitting review skills dispatched per phase, mirroring the
    //   pre-collapse discussion machinery that is still alive for autonomous.
    // - CompilePrReviewFindings + PostPrComments handlers land in p0167c; the
    //   steps are declared here so the preset shape is final from day one.
    //   WriteRunResult runs BEFORE PostPrComments (write-then-deliver, like
    //   fix-bug) so the summary comment can link to an existing result.md.
    public static readonly IReadOnlyList<string> PrReview =
    [
        CommandNames.LoadCatalog,
        CommandNames.PipelineNameInitializer,
        CommandNames.CheckoutSource,
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate,
        CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
        CommandNames.AnalyzeCode,
        CommandNames.AnalyzePrDiff,
        CommandNames.LoadSkills,
        CommandNames.Triage,
        CommandNames.RunReviewPhase,
        CommandNames.CompilePrReviewFindings,
        CommandNames.WriteRunResult,
        CommandNames.PostPrComments,
    ];
}
