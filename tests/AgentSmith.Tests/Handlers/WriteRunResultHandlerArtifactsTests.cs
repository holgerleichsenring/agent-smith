using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Dialogue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AgentSmith.Application.Services.Persistence;

namespace AgentSmith.Tests.Handlers;

public sealed class WriteRunResultHandlerArtifactsTests
{
    private readonly InMemoryRunArtifactStore _store = new();
    private readonly WriteRunResultHandler _sut;

    public WriteRunResultHandlerArtifactsTests()
    {
        _sut = new WriteRunResultHandler(new InMemoryDialogueTrail(), _store, new AgentSmith.Tests.Events.RecordingEventPublisher(), AgentSmith.Tests.TestHelpers.MemoryOutcomes.None(), AgentSmith.Tests.TestHelpers.TestRunResultChanges.Create(), NullLogger<WriteRunResultHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_PerSkillBreakdownPresent_RendersBreakdownSection()
    {
        var pipeline = NewPipelineWithSandbox();
        var tracker = PipelineCostTracker.GetOrCreate(pipeline);
        var scope = tracker.BeginCall("plan-author", "planner", SkillExecutionPhase.Plan);
        scope.Dispose();

        await _sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var result = await Result();
        result.Should().Contain("## Per-skill cost breakdown");
        result.Should().Contain("plan-author (planner, Plan)");
    }

    [Fact]
    public async Task ExecuteAsync_PerSkillBreakdownEmpty_NoBreakdownSection()
    {
        var pipeline = NewPipelineWithSandbox();

        await _sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var result = await Result();
        result.Should().NotContain("Per-skill cost breakdown");
    }

    [Fact]
    public async Task ExecuteAsync_CodingRunWithoutARecordedPlan_StoresNoPlan()
    {
        // 2026-11-28-db88b: a coding master records its plan with record_plan; nothing is read back
        // from the sandbox, so a run whose master recorded none has no plan.
        var pipeline = NewPipelineWithSandbox();
        var repo = new Repository(new BranchName("feature/x"), "https://example.test/x");
        var ticket = new Ticket(new TicketId("99"), "fix: the bug", "desc", null, "Open", "github");
        var ctx = new WriteRunResultContext(repo, Plan: null, ticket, Array.Empty<CodeChange>(), pipeline);

        await _sut.ExecuteAsync(ctx, CancellationToken.None);

        (await _store.ReadPlanMarkdownAsync(RunId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FailureReasonInContext_ResultMdLeadsWithOutcome()
    {
        // p0237: a failed/cancelled run still records — result.md must lead with
        // an Outcome section naming WHY, not silently look like a success.
        var pipeline = NewPipelineWithSandbox();
        pipeline.Set(ContextKeys.FailureReason,
            "The coding agent's LLM request timed out (network_timeout_seconds).");

        await _sut.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var result = await Result();
        result.Should().Contain("## Outcome");
        result.Should().Contain("did not complete");
        result.Should().Contain("network_timeout_seconds");
    }

    private const string RunId = "2026-05-20T22-27-43-8a3f";

    private async Task<string> Result() => (await _store.ReadResultMarkdownAsync(RunId, CancellationToken.None))!;

    private static PipelineContext NewPipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, Mock.Of<ISandbox>());
        pipeline.Set(ContextKeys.RunId, RunId);
        return pipeline;
    }

    private static WriteRunResultContext NewContext(PipelineContext pipeline)
    {
        var repo = new Repository(new BranchName("feature/x"), "https://example.test/x");
        var ticket = new Ticket(new TicketId("99"), "Add caching layer", "desc", null, "Open", "github");
        var plan = new Plan("Summary", Array.Empty<PlanStep>(), "{}");
        return new WriteRunResultContext(repo, plan, ticket, Array.Empty<CodeChange>(), pipeline);
    }
}
