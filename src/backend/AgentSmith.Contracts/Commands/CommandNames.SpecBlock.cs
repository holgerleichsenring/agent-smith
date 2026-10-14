namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0437: the command names of the block a run splices ONCE PER DERIVED SPEC. They are
/// lifted out of the pipeline list because their ORDER is a contract, not a catalogue
/// entry: the work must reach the branch before the gate that judges it, and
/// PipelinePresets.CodeSpecBlock is asserted against that rule.
/// </summary>
public static partial class CommandNames
{
    /// <summary>p0315d: dogfoods the methodology — writes the executed spec
    /// spec to the target repo's .agentsmith/specs/done/ inside the sandbox
    /// working tree so CommitAndPR ships it with the change set.</summary>
    public const string WriteSpecRecord = "WriteSpecRecordCommand";

    /// <summary>p0393: runs the repository's own build and test commands after the master
    /// and fails the run before any PR when either is red. p0216 gave build+test to the
    /// coding master as a RESPONSIBILITY and left nothing that refuses a PR on a red build,
    /// so "green" was a model claim with no second opinion. The commands come from
    /// ProjectMap.Ci (populated by AnalyzeCode) — this step needs no discovery of its own.</summary>
    public const string VerifySpec = "VerifySpecCommand";
    public const string WriteRunResult = "WriteRunResultCommand";
    public const string CommitSpecWork = "CommitSpecWorkCommand"; // p0437: before the gate

    /// <summary>2026-09-17-0e79c: asks a fresh instance whether the premises the spec STATES
    /// still hold in the repositories, before its work starts. A false one fails the step and
    /// hands the spec back; the check reports and never rewrites the spec.</summary>
    public const string CheckSpecPremises = "CheckSpecPremisesCommand";

    /// <summary>2026-09-17-042eh: after the verification, a fresh instance reads the spec's
    /// own diff against the spec and the loaded principles. VerifySpec asks whether the
    /// done-list is satisfied; nothing asked whether what was written is sound. Findings that
    /// rest on a read the reviewer took get ONE fix pass and are recorded either way.</summary>
    public const string ReviewSpecDiff = "ReviewSpecDiffCommand";

    /// <summary>2026-10-13-8762g: before the master builds, each unbuilt spec a person did not
    /// approve as written is attacked by fresh reviewers, round by round, its cuts applied.</summary>
    public const string ReviewSpecs = "ReviewSpecsCommand";
}
