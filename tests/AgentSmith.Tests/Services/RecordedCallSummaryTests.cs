using System.Text;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Events;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-11-17-7c9bd: result.md's cost sections are summed from the run's recorded calls — every call,
/// worker calls included, as the run row sums them — not from the tracker's partial view.
/// </summary>
public sealed class RecordedCallSummaryTests
{
    [Fact]
    public void Of_WorkerAndProviderCallsAcrossStagesAndRepos_SumsEverything()
    {
        IReadOnlyList<RecordedCall> calls =
        [
            new("claude-opus-5-5", "Plan", "server", 10, 100, 1000, 50, 0.50m, IsWorker: true),
            new("claude-opus-5-5", "Implementation", "client", 20, 200, 2000, 0, 1.25m, IsWorker: true),
            new("gpt-4.1", null, null, 5, 5, 0, 0, 0.05m, IsWorker: false),
        ];

        var (stages, total, perRepo, unpriced, worker) = RecordedCallSummary.Of(calls, RecordedCallCarry.None, null)!;

        total.Should().Be(1.80m);
        stages.Keys.Should().BeEquivalentTo(["Plan", "Implementation", "Other"]);
        stages["Other"].Model.Should().Be("gpt-4.1");
        perRepo!.Keys.Should().BeEquivalentTo(["server", "client"]);
        perRepo["client"].TotalCost.Should().Be(1.25m);
        unpriced.Should().BeNull();
        worker!.CallCount.Should().Be(2);
        worker.ReportedCostUsd.Should().Be(1.75m);
        worker.CacheCreationTokens.Should().Be(50);
        worker.Models.Should().Be("claude-opus-5-5");
    }

    [Fact]
    public void Of_ACarryFromEarlierSegments_AddsToTheTotalAsItsOwnRow()
    {
        IReadOnlyList<RecordedCall> calls = [new("m", "Plan", null, 1, 1, 0, 0, 0.40m, IsWorker: true)];

        var (stages, total, _, _, worker) = RecordedCallSummary.Of(calls, new RecordedCallCarry(7, 8, 9, 0, 1.00m), null)!;

        total.Should().Be(1.40m);
        stages["Prior segments"].Cost.Should().Be(1.00m);
        worker!.CallCount.Should().Be(1, "a carry is not a call");
    }

    [Fact]
    public void Of_NoCall_Null() =>
        RecordedCallSummary.Of([], RecordedCallCarry.None, null).Should().BeNull();

    /// <summary>The replay's shape: 36 recorded worker calls, the tracker having counted 29 and priced none.</summary>
    [Fact]
    public void ResultFrontmatter_ThirtySixWorkerCalls_RendersTheirTokensCostAndCount()
    {
        var calls = Enumerable.Range(0, 36)
            .Select(_ => new RecordedCall("claude-opus-5-5", "Implementation", null, 2, 2000, 50_000, 0, 0.75m, IsWorker: true))
            .ToList();
        var sb = new StringBuilder();

        RunCostSectionWriter.AppendFrontmatter(sb, new Ticket(new TicketId("SCRUM-43"), "t", "d", null, "open", "jira", []),
            "feat", 60, RecordedCallSummary.Of(calls, RecordedCallCarry.None, null));

        var text = sb.ToString();
        text.Should().Contain("total_usd: 27.0000").And.Contain("    calls: 36").And.Contain("  output: 72000");
        text.Should().NotContain("total_usd: 0.0000");
    }

    /// <summary>2026-11-17-7c9bf: haiku's side use is summed per model and named beside the worker block.</summary>
    [Fact]
    public void ResultFrontmatter_SideModel_RendersASecondaryLineAndKeepsTheModelName()
    {
        var side = new AgentSmith.Contracts.Models.Workers.WorkerModelUsage("claude-haiku-4-5-20251001", 400, 20, 0, 0, 0.0005m);
        var calls = Enumerable.Range(0, 2)
            .Select(_ => new RecordedCall("claude-opus-5-5", "Plan", null, 2, 900, 0, 0, 0.42m, IsWorker: true) { Secondary = [side] })
            .ToList();
        var sb = new StringBuilder();

        RunCostSectionWriter.AppendFrontmatter(sb, new Ticket(new TicketId("1"), "t", "d", null, "open", "jira", []),
            "feat", 1, RecordedCallSummary.Of(calls, RecordedCallCarry.None, null));

        sb.ToString().Should().Contain("    model: claude-opus-5-5\n")
            .And.Contain("    secondary: claude-haiku-4-5-20251001 (800 in, 40 out, 0 cache read, 0.0010 usd)");
    }
}
