namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0179b: collapsed shape — Triage / GeneratePlan / PlanOpenQuestions /
    // EmptyPlanCheck / RunReviewPhase / RunFinalPhase retired from this preset.
    // coding-agent-master handles plan + execute internally; no Test step
    // (the variant's whole point is to skip the verify gate for trivial fixes).
    public static readonly IReadOnlyList<string> FixNoTest =
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
        // p0390: this preset negotiates no expectation, so the work spec's own
        // done-criteria are the run's ONLY criteria list — and revisable, unlike the
        // read-only section a ratified expectation produces. It has no plan of its own;
        // the spec is the statement of the work the master reads.
        CommandNames.DeriveSpecification,
        // p0390: the hand-back router runs immediately after derivation, so a ticket
        // that cannot be specified never reaches the plan or the master.
        CommandNames.WorkSpecHandback,
        CommandNames.EnsurePrerequisites, // p0202e: after AnalyzeCode (analyzer-derived command), before master
        CommandNames.Approval, CommandNames.AgenticMaster,
        // p0391: mid-run clarification park — see PipelinePresets.FixBug. This preset plans
        // from scratch (no PlanOpenQuestions gate), so this is its ONLY question exit.
        CommandNames.MasterOpenQuestions,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
