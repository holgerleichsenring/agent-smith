namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0437: the command names of the block a run splices ONCE PER DERIVED PHASE. They are
/// lifted out of the pipeline list because their ORDER is a contract, not a catalogue
/// entry: the work must reach the branch before the gate that judges it, and
/// PipelinePresets.CodePhaseBlock is asserted against that rule.
/// </summary>
public static partial class CommandNames
{
    /// <summary>p0315d: dogfoods the methodology — writes the executed phase
    /// spec to the target repo's .agentsmith/phases/done/ inside the sandbox
    /// working tree so CommitAndPR ships it with the change set.</summary>
    public const string WritePhaseRecord = "WritePhaseRecordCommand";

    /// <summary>p0393: runs the repository's own build and test commands after the master
    /// and fails the run before any PR when either is red. p0216 gave build+test to the
    /// coding master as a RESPONSIBILITY and left nothing that refuses a PR on a red build,
    /// so "green" was a model claim with no second opinion. The commands come from
    /// ProjectMap.Ci (populated by AnalyzeCode) — this step needs no discovery of its own.</summary>
    public const string VerifyPhase = "VerifyPhaseCommand";
    public const string WriteRunResult = "WriteRunResultCommand";
    public const string CommitPhaseWork = "CommitPhaseWorkCommand"; // p0437: before the gate
}
