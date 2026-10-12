using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0438: an outstanding criterion goes back to the agent that can close it, once, before it
/// becomes the operator's problem.
/// <para>
/// The operator's question named the defect: "a failed run where the accounting is right —
/// what good is that to me?" None. The accountant produces the most actionable artefact of a
/// run, and it was being rendered into an error message while the agent that wrote the work
/// never saw it.
/// </para>
/// </summary>
public sealed class SpecRepairTests
{
    [Fact]
    public void OutstandingCriteria_AreRepairable()
    {
        var accounts = new[] { Account(outstanding: ["the inventory enumerates all handlers"]) };

        SpecVerdict.IsRepairable(accounts).Should().BeTrue();
        SpecVerdict.Outstanding(accounts).Should().ContainSingle()
            .Which.Should().Contain("all handlers");
    }

    /// <summary>
    /// A red build or an unaccounted spec is not repairable this way: an account taken over
    /// a tree that does not compile is an opinion about work nobody can ship (p0420), and a
    /// repair pass would answer a question the compiler already answered.
    /// </summary>
    [Fact]
    public void AnUnaccountedSpec_IsNeverHandedBack()
    {
        var accounts = new[] { Account(outstanding: ["x"], problem: "the accountant returned no verdict") };

        SpecVerdict.IsRepairable(accounts).Should().BeFalse();
    }

    [Fact]
    public void ASatisfiedSpec_IsNotRepairable()
        => SpecVerdict.IsRepairable([Account(outstanding: [])]).Should().BeFalse();

    [Fact]
    public void NoAccounts_IsNotRepairable()
        => SpecVerdict.IsRepairable([]).Should().BeFalse();

    /// <summary>
    /// The failure text a still-unsatisfied spec produces must be the one it produced before
    /// p0438 — the honesty of p0419/p0420 is untouched, it just stops being the first answer.
    /// </summary>
    [Fact]
    public void OutstandingCriteria_StillNameThemselvesInTheVerdict()
    {
        var verdict = SpecVerdict.From(
            CommandResult.Ok("build green"),
            [Account(outstanding: ["the inventory enumerates all handlers"])]);

        verdict.IsSuccess.Should().BeFalse();
        verdict.Message.Should().Contain("not satisfied by the branch")
            .And.Contain("all handlers");
    }

    private static SpecAccount Account(string[] outstanding, string? problem = null) =>
        new(
            RepoKey: "server",
            Criteria: [.. outstanding.Select(c => new CriterionAccount(c, Disposition: AccountDisposition.NotSatisfied))],
            Problem: problem);

    /// <summary>
    /// The repair is only worth a master pass if the agent is told WHICH criteria the branch
    /// does not satisfy. A generic "try again" spends the pass and closes nothing, so the
    /// accountant's own words reach the prompt.
    /// </summary>
    [Fact]
    public void TheRepairPrompt_QuotesTheOutstandingCriteria()
    {
        var pipeline = new AgentSmith.Contracts.Commands.PipelineContext();
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.OutstandingCriteria,
            new List<string> { "server: the inventory enumerates all MediatR handlers" });

        var block = AgentSmith.Application.Services.SpecExecutionPromptBlocks
            .OutstandingCriteria(pipeline);

        block.Should().Contain("REPAIR pass")
            .And.Contain("the inventory enumerates all MediatR handlers")
            .And.Contain("Close exactly these",
                "widening the repair into the rest of the phase is scope it was not given");
    }

    [Fact]
    public void WithNothingOutstanding_TheRepairBlockIsAbsent()
        => AgentSmith.Application.Services.SpecExecutionPromptBlocks
            .OutstandingCriteria(new AgentSmith.Contracts.Commands.PipelineContext())
            .Should().BeEmpty();

    /// <summary>
    /// p0341g: a repeated step belongs to the spec it repeats.
    /// <para>
    /// Proven by live run a98c, whose step projection came back
    /// <c>23 spec=p19106a / 24 spec=None / 25 spec=None / 26 spec=None / 27 spec=p19106a</c>
    /// — the rail groups CONTIGUOUS spec ids, so the un-stamped repair broke the run into
    /// two p19106a groups, and the pass's $0.63 and 20 model calls were rolled up under no
    /// spec at all. SpecSequence stamps every step it splices; the repair must too.
    /// </para>
    /// </summary>
    [Fact]
    public void RepairSteps_CarryTheSpecTheyRepair()
        => SpecVerdict.RepairSteps("p19106a").Should()
            .OnlyContain(c => c.SpecId == "p19106a")
            // The count tracks the block: a literal here would have to be edited every
            // time the block changes, and p0449 is exactly that moment.
            .And.HaveCount(SpecVerdict.RepairBlock.Count);

    /// <summary>
    /// A run outside a spec sequence has no spec to belong to, and inventing one would put
    /// steps under a heading no spec ever wrote.
    /// </summary>
    [Fact]
    public void OutsideASequence_TheRepairCarriesNoSpec()
        => SpecVerdict.RepairSteps(null).Should().OnlyContain(c => c.SpecId == null);

    /// <summary>
    /// The repair REPEATS part of the spec block. A name that is not in the block would be a
    /// step the spec never runs — a pipeline the repair invented for itself, and the way the
    /// two drift apart the next time the block is edited (p0437 edited it once already).
    /// </summary>
    [Fact]
    public void EveryRepairedStep_IsAStepTheSpecActuallyRuns()
        => SpecVerdict.RepairBlock.Should().BeSubsetOf(PipelinePresets.CodeSpecBlock);

    /// <summary>
    /// p0449: a repair pass can ask, so a repair pass must be able to be answered.
    /// <para>
    /// Live run 459d: the first pass left two criteria outstanding, the repair ran, and its
    /// step reported "awaiting_user_input: master asked for clarification mid-run". The
    /// next step was Commit. The repair repeated work, branch and verdict, and skipped the
    /// one step through which a question reaches the operator — so the question stayed in
    /// the bag, the parking flag was never set, no checkpoint was written, and the run went
    /// on to fail on the very criteria it had asked about.
    /// </para>
    /// <para>
    /// Asking rather than guessing is the behaviour the whole gate exists to produce. It
    /// must not be worth less on the second pass than on the first.
    /// </para>
    /// </summary>
    [Fact]
    public void ARepairThatAsks_StillReachesTheOperator()
        => SpecVerdict.RepairBlock.Should().Contain(CommandNames.MasterOpenQuestions,
            "a question the repair captured and nobody posts is a question nobody asked");

    /// <summary>
    /// The repeated steps stay in the order the spec runs them: the question is posted
    /// after the work that raised it and before the branch is judged.
    /// </summary>
    [Fact]
    public void TheRepairRepeatsTheBlocksOwnOrder()
        => SpecVerdict.RepairBlock.Should().ContainInOrder(
            CommandNames.AgenticMaster, CommandNames.MasterOpenQuestions,
            CommandNames.CommitSpecWork, CommandNames.VerifySpec);
}
