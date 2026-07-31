namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // p0179b: collapsed shape — Triage / GeneratePlan / PlanOpenQuestions /
    // EmptyPlanCheck / RunReviewPhase / RunFinalPhase / RunVerifyPhase retired
    // from this preset. coding-agent-master handles plan + execute + verify
    // in one agentic loop. GenerateTests + GenerateDocs stay — they are
    // separate post-master responsibilities.
    public static readonly IReadOnlyList<string> AddFeature =
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
        // p0328: negotiate the WHAT before planning — the draft is grounded in
        // analysis, not the raw ticket; the ratified expectation is the run's
        // acceptance contract.
        CommandNames.NegotiateExpectation,
        CommandNames.EnsurePrerequisites, // p0202e: after AnalyzeCode (analyzer-derived command), before master
        // p0276: plan generated + approved BEFORE execution; the master executes it.
        // p0390: DeriveSpecification turns the ticket into a versioned work spec —
        // requirements and verbatim constraints, NEVER steps — and commits it to the
        // ticket branch before any source edit. It runs BEFORE GeneratePlan because the
        // plan is derived from the spec's requirements; steps and target files stay the
        // plan's, and the ledger keeps seeding from the plan.
        CommandNames.DeriveSpecification,
        // p0390: the hand-back router runs immediately after derivation, so a ticket
        // that cannot be specified never reaches the plan or the master.
        CommandNames.WorkSpecHandback,
        CommandNames.GeneratePlan,
        // p0318: clarification gate — halts + parks a title-only / needs-input ticket
        // before the master. Re-added after the p0179b collapse.
        CommandNames.PlanOpenQuestions,
        CommandNames.Approval,
        // p0216: the rigid projectmap-derived Test step was removed — the
        // coding-agent-master owns build+test verification via its real
        // run_command calls. GenerateTests + GenerateDocs stay (separate
        // post-master responsibilities).
        CommandNames.AgenticMaster,
        // p0391: mid-run clarification park — see PipelinePresets.FixBug. Placed directly
        // after the master so a parked run generates no tests and no docs either.
        CommandNames.MasterOpenQuestions,
        CommandNames.GenerateTests,
        CommandNames.GenerateDocs,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
