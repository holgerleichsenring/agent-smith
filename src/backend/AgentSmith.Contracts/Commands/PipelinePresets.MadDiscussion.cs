namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // Multi-agent-discussion preset: fetches the ticket, loads context + skills,
    // triages roles, runs a ConvergenceCheck loop (no AgenticExecute), compiles the
    // discussion transcript, and writes the result + PR. No code changes — output is
    // the transcript itself, committed for downstream review.
    public static readonly IReadOnlyList<string> MadDiscussion =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.FetchTicket, CommandNames.CheckoutSource,
        CommandNames.LoadContext,
        CommandNames.LoadSkills, // p0137a: AvailableRoles for StructuredTriageStrategy
        CommandNames.Triage,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileDiscussion,
        CommandNames.WriteRunResult, CommandNames.CommitAndPR,
        CommandNames.PrCrossLink, // p0158c: multi-repo pass-2 (no-op for single-PR runs)
    ];
}
