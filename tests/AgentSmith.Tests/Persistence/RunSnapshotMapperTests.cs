using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0246f: the persisted Run (read from the DB) maps to the dashboard's
/// RunSnapshot contract, so the run list/detail can be served from the
/// system-of-record (survives restart + Redis flush), not just the in-memory
/// broadcaster snapshots.
/// </summary>
public sealed class RunSnapshotMapperTests
{
    [Fact]
    public void ToSnapshot_MapsRunWithChildren_ToDashboardContract()
    {
        var run = new Run
        {
            Id = "run-1", Pipeline = "fix-bug", Trigger = "ticket", Status = "success",
            Summary = "Fixed it", StartedAt = DateTimeOffset.Parse("2026-06-07T10:00:00Z"),
            FinishedAt = DateTimeOffset.Parse("2026-06-07T10:05:00Z"), CostTotalUsd = 0.07m,
            TicketId = "42", TicketTitle = "The bug", AgentName = "claude", CancelRequested = false,
            Repos = [new RunRepo { RepoName = "primary", PrStatus = "opened", PrUrl = "https://pr/1" }],
            Steps =
            [
                new RunStep { StepIndex = 0, StepName = "LoadCatalog", Status = "ok" },
                new RunStep { StepIndex = 5, StepName = "AgenticMaster", DisplayName = "Agent", Status = "ok" },
            ],
            LlmCalls = [new RunLlmCall { Model = "gpt-4.1" }, new RunLlmCall { Model = "gpt-4.1" }],
            Sandboxes = [new RunSandbox { Key = "primary" }],
        };

        var snap = RunSnapshotMapper.ToSnapshot(run);

        snap.RunId.Should().Be("run-1");
        snap.Pipeline.Should().Be("fix-bug");
        snap.Status.Should().Be("success");
        snap.Summary.Should().Be("Fixed it");
        snap.Repos.Should().ContainSingle().Which.Should().Be("primary");
        snap.PrUrl.Should().Be("https://pr/1", "the opened PR's url surfaces");
        snap.StepIndex.Should().Be(5, "the latest step by index");
        snap.StepName.Should().Be("Agent", "the display name is preferred over the raw step name");
        snap.CostUsd.Should().Be(0.07m);
        snap.LlmCalls.Should().Be(2);
        snap.Sandboxes.Should().Be(1);
        snap.TicketId.Should().Be("42");
        snap.TicketTitle.Should().Be("The bug");
        snap.AgentName.Should().Be("claude");
        snap.Title.Should().Be("The bug", "the ticket title is the dashboard heading");
    }

    [Fact]
    public void ToSnapshot_EmptyTicketId_MapsToNull_TitleFallsBackToPipeline()
    {
        var run = new Run { Id = "r", Pipeline = "security-scan", Status = "running", TicketId = "" };

        var snap = RunSnapshotMapper.ToSnapshot(run);

        snap.TicketId.Should().BeNull("an empty ticket id is absent, not a literal empty string");
        snap.Title.Should().Be("security-scan", "no ticket → the pipeline name is the title");
        snap.Trigger.Should().Be("unknown", "a null trigger maps to the contract's unknown");
    }
}
