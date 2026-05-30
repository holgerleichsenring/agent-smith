using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0179f: pins the Plan-is-nullable contract on ApprovalContext + Builder +
/// Handler so the collapsed coding presets (fix-bug / add-feature / fix-no-test)
/// reach Approval without crashing on a missing "Plan" pipeline key.
/// </summary>
public sealed class ApprovalNullPlanTests
{
    private readonly ApprovalHandler _handler = new(
        NullLoggerFactory.Instance.CreateLogger<ApprovalHandler>());

    [Fact]
    public void ApprovalContext_AcceptsNullPlan()
    {
        var pipeline = new PipelineContext();
        var ctx = new ApprovalContext(null, pipeline);
        ctx.Plan.Should().BeNull();
    }

    [Fact]
    public void ApprovalContextBuilder_PlanAbsent_BuildsContextWithNullPlan()
    {
        var pipeline = new PipelineContext();
        var project = CreateProjectConfig();

        var result = new ApprovalContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.Approval), project, pipeline);

        result.Should().BeOfType<ApprovalContext>();
        ((ApprovalContext)result).Plan.Should().BeNull();
    }

    [Fact]
    public void ApprovalContextBuilder_PlanPresent_BuildsContextWithPlan()
    {
        var plan = new Plan("Summary", new List<PlanStep>(), "{}");
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Plan, plan);
        var project = CreateProjectConfig();

        var result = new ApprovalContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.Approval), project, pipeline);

        ((ApprovalContext)result).Plan.Should().BeSameAs(plan);
    }

    [Fact]
    public async Task ApprovalHandler_NullPlan_Headless_AutoApprovesAndSetsApprovedTrue()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Headless, true);
        pipeline.Set(ContextKeys.Ticket,
            new Ticket(new TicketId("42"), "Title", "Desc", null, "Open", "GitHub"));
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "primary", Type = RepoType.Local, Path = "/tmp" }]);
        var context = new ApprovalContext(null, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<bool>(ContextKeys.Approved).Should().BeTrue();
    }

    private static ResolvedProject CreateProjectConfig() => new()
    {
        Repos = [new RepoConnection { Name = "primary", Type = RepoType.Local, Path = "/tmp" }],
        Tracker = new TrackerConnection { Type = TrackerType.GitHub, Url = "https://github.com/x/y" },
        Agent = new AgentConfig { Type = "claude", Model = "sonnet" },
        Pipeline = "fix-bug",
        CodingPrinciplesPath = "config/coding-principles.md",
    };
}
