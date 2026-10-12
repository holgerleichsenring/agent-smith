using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0437: a gate runs AFTER the thing it judges.
/// <para>
/// Measured live on ticket 19106: the master wrote its file, the spec verification read
/// the branch, reported "this branch carries no source change", and called five satisfied
/// criteria outstanding — and the work reached the branch afterwards, in the run-level
/// CommitAndPR that runs once after ALL specs. Not a race: the gate simply stood before
/// the delivery, every run, every spec.
/// </para>
/// <para>
/// The operator named the consequence, and it is why this is a rule rather than a fix:
/// with the gate before the delivery, work that has been done can NEVER reach its goal.
/// The next person to reorder this block will not have read the spec, so the order
/// is asserted instead of documented.
/// </para>
/// </summary>
public sealed class GateOrderRuleTests
{
    private static readonly string[] CommittingSteps =
        [CommandNames.CommitSpecWork, CommandNames.CommitAndPR, CommandNames.InitCommit];

    [Fact]
    public void SpecBlock_TheVerificationFollowsACommittingStep()
    {
        var block = PipelinePresets.CodeSpecBlock;
        var verify = block.ToList().IndexOf(CommandNames.VerifySpec);

        verify.Should().BeGreaterThan(-1, "the spec block is what splices per derived spec");
        var commitBefore = block.Take(verify).Any(CommittingSteps.Contains);

        commitBefore.Should().BeTrue(
            "the spec verification reads the BRANCH (p0422), so the spec's work has to be "
            + "on it first — otherwise a spec that delivered is accounted for as having "
            + "delivered nothing. Steps before the gate: "
            + string.Join(" -> ", block.Take(verify)));
    }

    [Fact]
    public void SpecBlock_CommitsAfterTheMasterHasWorked()
    {
        var block = PipelinePresets.CodeSpecBlock.ToList();

        block.IndexOf(CommandNames.CommitSpecWork).Should().BeGreaterThan(
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
            CommandNames.SelectSpec, CommandNames.AgenticMaster,
            CommandNames.MasterOpenQuestions, CommandNames.VerifySpec,
            CommandNames.WriteSpecRecord,
        ];

        var verify = asItWas.ToList().IndexOf(CommandNames.VerifySpec);

        asItWas.Take(verify).Any(CommittingSteps.Contains).Should().BeFalse(
            "the pre-p0437 order had no committing step before the gate — that is the defect");
    }
}
