namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // Document-analysis preset: AcquireSource pulls the legal document, BootstrapDocument
    // converts it to markdown, the triage + discussion loop applies the legal-* skill
    // catalog, and DeliverOutput writes the final analysis (no commit — output is
    // a document deliverable, not a repo change).
    public static readonly IReadOnlyList<string> LegalAnalysis =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.AcquireSource,
        CommandNames.BootstrapDocument,
        CommandNames.LoadCodingPrinciples,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.Triage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.DeliverOutput,
    ];
}
