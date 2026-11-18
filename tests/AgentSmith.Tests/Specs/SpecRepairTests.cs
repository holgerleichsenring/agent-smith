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

        block.Should().Contain("ran red").And.Contain("the inventory enumerates all MediatR handlers");
    }

    /// <summary>2026-10-17-a196a: the repair is told which repositories are its own.</summary>
    [Fact]
    public void OutstandingCriteria_SpecNamingRepos_NamesThem()
    {
        var pipeline = new AgentSmith.Contracts.Commands.PipelineContext();
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.OutstandingCriteria, new List<string> { "server: c" });
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.CurrentSpec,
            new AgentSmith.Contracts.Models.SpecDraft("p1", "g", "spec: p1", []) { Repos = ["server"] });

        AgentSmith.Application.Services.SpecExecutionPromptBlocks.OutstandingCriteria(pipeline)
            .Should().Contain("This spec is written into server alone");
    }

    [Fact]
    public void WithNothingOutstanding_TheRepairBlockIsAbsent()
        => AgentSmith.Application.Services.SpecExecutionPromptBlocks
            .OutstandingCriteria(new AgentSmith.Contracts.Commands.PipelineContext())
            .Should().BeEmpty();

}
