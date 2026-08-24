using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The commands that cannot run without a sandbox. Extracted from
/// PipelineSandboxCoordinator (p0504): which commands need a pod is a fact about
/// the command set, not about the lifecycle that provisions one.
/// </summary>
public static class SandboxRequiringCommands
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        CommandNames.CheckoutSource, CommandNames.AcquireSource,
        CommandNames.AgenticExecute,
        CommandNames.GenerateTests, CommandNames.GenerateDocs,
        CommandNames.CommitAndPR, CommandNames.InitCommit, CommandNames.PersistWorkBranch,
        CommandNames.BootstrapProject, CommandNames.BootstrapDocument, CommandNames.BootstrapCheck,
        CommandNames.BootstrapDiscover, // p0161d: read-only LLM round reads via the per-repo sandbox
        CommandNames.LoadContext, CommandNames.LoadCodingPrinciples, CommandNames.LoadCodeMap,
        CommandNames.LoadRuns, CommandNames.AnalyzeCode,
        CommandNames.CompileDiscussion, CommandNames.CompileKnowledge, CommandNames.QueryKnowledge,
        CommandNames.WriteRunResult,
        CommandNames.StaticPatternScan, CommandNames.GitHistoryScan, CommandNames.DependencyAudit,
        CommandNames.SecurityTrend, CommandNames.SecuritySnapshotWrite, CommandNames.SpawnFix
    };

    public static bool Contains(string commandName) => Names.Contains(commandName);
}
