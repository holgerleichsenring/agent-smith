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
public sealed class PhaseRepairTests
{
    [Fact]
    public void OutstandingCriteria_AreRepairable()
    {
        var accounts = new[] { Account(outstanding: ["the inventory enumerates all handlers"]) };

        PhaseVerdict.IsRepairable(accounts).Should().BeTrue();
        PhaseVerdict.Outstanding(accounts).Should().ContainSingle()
            .Which.Should().Contain("all handlers");
    }

    /// <summary>
    /// A red build or an unaccounted phase is not repairable this way: an account taken over
    /// a tree that does not compile is an opinion about work nobody can ship (p0420), and a
    /// repair pass would answer a question the compiler already answered.
    /// </summary>
    [Fact]
    public void AnUnaccountedPhase_IsNeverHandedBack()
    {
        var accounts = new[] { Account(outstanding: ["x"], problem: "the accountant returned no verdict") };

        PhaseVerdict.IsRepairable(accounts).Should().BeFalse();
    }

    [Fact]
    public void ASatisfiedPhase_IsNotRepairable()
        => PhaseVerdict.IsRepairable([Account(outstanding: [])]).Should().BeFalse();

    [Fact]
    public void NoAccounts_IsNotRepairable()
        => PhaseVerdict.IsRepairable([]).Should().BeFalse();

    /// <summary>
    /// The failure text a still-unsatisfied phase produces must be the one it produced before
    /// p0438 — the honesty of p0419/p0420 is untouched, it just stops being the first answer.
    /// </summary>
    [Fact]
    public void OutstandingCriteria_StillNameThemselvesInTheVerdict()
    {
        var verdict = PhaseVerdict.From(
            CommandResult.Ok("build green"),
            [Account(outstanding: ["the inventory enumerates all handlers"])]);

        verdict.IsSuccess.Should().BeFalse();
        verdict.Message.Should().Contain("not satisfied by the branch")
            .And.Contain("all handlers");
    }

    private static SpecAccount Account(string[] outstanding, string? problem = null) =>
        new(
            RepoKey: "server",
            Criteria: [.. outstanding.Select(c => new CriterionAccount(c, Satisfied: false))],
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

        var block = AgentSmith.Application.Services.PhaseExecutionPromptBlocks
            .OutstandingCriteria(pipeline);

        block.Should().Contain("REPAIR pass")
            .And.Contain("the inventory enumerates all MediatR handlers")
            .And.Contain("Close exactly these",
                "widening the repair into the rest of the phase is scope it was not given");
    }

    [Fact]
    public void WithNothingOutstanding_TheRepairBlockIsAbsent()
        => AgentSmith.Application.Services.PhaseExecutionPromptBlocks
            .OutstandingCriteria(new AgentSmith.Contracts.Commands.PipelineContext())
            .Should().BeEmpty();
}
