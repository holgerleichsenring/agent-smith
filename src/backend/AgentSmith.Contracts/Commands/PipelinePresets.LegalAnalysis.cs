namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // Document-analysis preset: AcquireSource pulls the legal document, BootstrapDocument
    // converts it to markdown, the triage + discussion loop applies the legal-* skill
    // catalog, and DeliverOutput writes the final analysis (no commit — output is
    // a document deliverable, not a repo change).
    // p0179d: collapsed shape. Triage / ConvergenceCheck / CompileDiscussion
    // retired FROM THIS PRESET. AgenticMaster loads legal-analyst-master.
    public static readonly IReadOnlyList<string> LegalAnalysis =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.AcquireSource,
        CommandNames.BootstrapDocument,
        CommandNames.LoadCodingPrinciples,
        CommandNames.AgenticMaster,         // p0179d: loads legal-analyst-master per pipeline-name routing
        CommandNames.DeliverOutput,
    ];
}
