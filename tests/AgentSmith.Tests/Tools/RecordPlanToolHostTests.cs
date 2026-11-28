using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Tools;

/// <summary>2026-11-28-db88b: the coding master's plan reaches the run page through record_plan.</summary>
public sealed class RecordPlanToolHostTests
{
    private const string RunId = "2026-11-28T10-00-00-db88";
    private readonly InMemoryRunArtifactStore _store = new();

    [Fact]
    public async Task RecordPlan_StoresThePlanForTheRunPage()
    {
        var answer = await Host().RecordPlan("# Plan\n- src/Api.cs: the guard");

        answer.Should().StartWith("Plan recorded");
        (await _store.ReadPlanMarkdownAsync(RunId, CancellationToken.None)).Should().Be("# Plan\n- src/Api.cs: the guard");
    }

    [Fact]
    public async Task RecordPlan_ALaterCall_ReplacesThePlan()
    {
        await Host().RecordPlan("# First");
        await Host().RecordPlan("# Second");

        (await _store.ReadPlanMarkdownAsync(RunId, CancellationToken.None)).Should().Be("# Second");
    }

    [Fact]
    public async Task RecordPlan_EmptyPlan_IsRefused()
    {
        (await Host().RecordPlan("  ")).Should().StartWith("Error:");
        (await _store.ReadPlanMarkdownAsync(RunId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public void GetTools_OffersRecordPlan() =>
        Host().GetTools(null, null).Select(t => t.Name).Should().Equal("record_plan");

    [Fact]
    public async Task RecordPlan_SeveralSpecsOfOneRun_EachKeepsItsOwnSection()
    {
        await Host("2026-11-28-aaaaa").RecordPlan("first plan of a");
        await Host("2026-11-28-aaaab").RecordPlan("plan of b");
        await Host("2026-11-28-aaaaa").RecordPlan("second plan of a");

        (await _store.ReadPlanMarkdownAsync(RunId, CancellationToken.None)).Should().Be(
            "## Plan for spec 2026-11-28-aaaaa\n\nsecond plan of a\n\n## Plan for spec 2026-11-28-aaaab\n\nplan of b\n\n");
    }

    private RecordPlanToolHost Host(string? specId = null) => new(_store, RunId, specId, NullLogger.Instance);
}
