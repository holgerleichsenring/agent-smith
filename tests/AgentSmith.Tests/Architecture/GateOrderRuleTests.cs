using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0437: a gate runs AFTER the thing it judges.
/// <para>
/// Measured live on ticket 19106: the master wrote its file, the phase verification read
/// the branch, reported "this branch carries no source change", and called five satisfied
/// criteria outstanding — and the work reached the branch afterwards, in the run-level
/// CommitAndPR that runs once after ALL phases. Not a race: the gate simply stood before
/// the delivery, every run, every phase.
/// </para>
/// <para>
/// The operator named the consequence, and it is why this is a rule rather than a fix:
/// with the gate before the delivery, work that has been done can NEVER reach its goal.
/// The next person to reorder this block will not have read the phase spec, so the order
/// is asserted instead of documented.
/// </para>
/// </summary>
public sealed class GateOrderRuleTests
{
    private static readonly string[] CommittingSteps =
        [CommandNames.CommitPhaseWork, CommandNames.CommitAndPR, CommandNames.InitCommit];

    [Fact]
    public void PhaseBlock_TheVerificationFollowsACommittingStep()
    {
        var block = PipelinePresets.CodePhaseBlock;
        var verify = block.ToList().IndexOf(CommandNames.VerifyPhase);

        verify.Should().BeGreaterThan(-1, "the phase block is what splices per derived phase");
        var commitBefore = block.Take(verify).Any(CommittingSteps.Contains);

        commitBefore.Should().BeTrue(
            "the phase verification reads the BRANCH (p0422), so the phase's work has to be "
            + "on it first — otherwise a phase that delivered is accounted for as having "
            + "delivered nothing. Steps before the gate: "
            + string.Join(" -> ", block.Take(verify)));
    }

    [Fact]
    public void PhaseBlock_CommitsAfterTheMasterHasWorked()
    {
        var block = PipelinePresets.CodePhaseBlock.ToList();

        block.IndexOf(CommandNames.CommitPhaseWork).Should().BeGreaterThan(
            block.IndexOf(CommandNames.AgenticMaster),
            "committing before the master runs would put nothing on the branch");
    }

    /// <summary>
    /// The rule has to fail on the shape that shipped, or it proves nothing. This is the
    /// block as it stood when the false negative was measured.
    /// </summary>
    [Fact]
    public void Rule_HasTeeth_AVerificationBeforeTheCommit_IsFlagged()
    {
        string[] asItWas =
        [
            CommandNames.SelectPhase, CommandNames.AgenticMaster,
            CommandNames.MasterOpenQuestions, CommandNames.VerifyPhase,
            CommandNames.WritePhaseRecord,
        ];

        var verify = asItWas.ToList().IndexOf(CommandNames.VerifyPhase);

        asItWas.Take(verify).Any(CommittingSteps.Contains).Should().BeFalse(
            "the pre-p0437 order had no committing step before the gate — that is the defect");
    }
}
