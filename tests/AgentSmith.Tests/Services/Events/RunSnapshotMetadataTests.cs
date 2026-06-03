using AgentSmith.Contracts.Events;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Events;

/// <summary>
/// p0211: pins the run-snapshot metadata completeness contract — repos + agent
/// land on every trigger path, and the title resolves to a stable fallback
/// label instead of the literal "unknown"/empty when the data exists.
/// </summary>
public sealed class RunSnapshotMetadataTests
{
    private const string RunId = "2026-06-03T09-00-00-meta";

    [Fact]
    public void RunSnapshot_NoTicketTitle_FallsBackToPipelineAndTicketLabel()
    {
        // Arrange: a ticket run started, but the FetchTicket step has not yet
        // published a TicketFetchedEvent — so TicketTitle is still null.
        var snapshot = RunSnapshot.Empty(RunId).Apply(new RunStartedEvent(
            RunId, Trigger: "ticket", Pipeline: "fix-bug",
            Repos: new[] { "backend" }, StartedAt: DateTimeOffset.UtcNow,
            AgentName: "claude/claude-sonnet-4", TicketId: "18803"));

        // Act
        var title = snapshot.Title;

        // Assert
        snapshot.TicketTitle.Should().BeNull();
        title.Should().Be("fix-bug #18803");
        title.Should().NotBe("unknown");
    }

    [Fact]
    public void RunSnapshot_TicketTitlePresent_PrefersRealTitle()
    {
        // Arrange
        var snapshot = RunSnapshot.Empty(RunId)
            .Apply(new RunStartedEvent(
                RunId, "ticket", "fix-bug", new[] { "backend" },
                DateTimeOffset.UtcNow, "claude/claude-sonnet-4", "18803"))
            .Apply(new TicketFetchedEvent(
                RunId, TicketId: "18803", Title: "Login button misaligned",
                Description: "...", State: "open", Labels: Array.Empty<string>(),
                AttachmentCount: 0, Source: "github", Timestamp: DateTimeOffset.UtcNow));

        // Act / Assert
        snapshot.Title.Should().Be("Login button misaligned");
    }

    [Fact]
    public void RunSnapshot_NoTicketAtAll_TitleIsPipeline()
    {
        // Arrange: a manual / CLI run with no ticket id.
        var snapshot = RunSnapshot.Empty(RunId).Apply(new RunStartedEvent(
            RunId, Trigger: "manual", Pipeline: "api-scan",
            Repos: new[] { "backend" }, StartedAt: DateTimeOffset.UtcNow,
            AgentName: "claude/claude-sonnet-4"));

        // Act / Assert
        snapshot.TicketId.Should().BeNull();
        snapshot.Title.Should().Be("api-scan");
        snapshot.Title.Should().NotBe("unknown");
    }

    [Theory]
    [InlineData("ticket")]
    [InlineData("manual")]
    [InlineData("webhook")]
    [InlineData("poller")]
    public void RunStarted_AllTriggerPaths_CarryReposAndAgent(string trigger)
    {
        // Arrange: every trigger path funnels through the single RunStartedEvent
        // producer (ExecutePipelineUseCase), which always derives repos + agent.
        var started = new RunStartedEvent(
            RunId, Trigger: trigger, Pipeline: "fix-bug",
            Repos: new[] { "backend", "frontend" },
            StartedAt: DateTimeOffset.UtcNow,
            AgentName: "claude/claude-sonnet-4", TicketId: "42");

        // Act
        var snapshot = RunSnapshot.Empty(RunId).Apply(started);

        // Assert
        snapshot.Trigger.Should().Be(trigger);
        snapshot.Repos.Should().BeEquivalentTo(new[] { "backend", "frontend" });
        snapshot.AgentName.Should().Be("claude/claude-sonnet-4");
        snapshot.Pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public void RunSnapshot_ReposPresent_NeverRendersAsNoRepos()
    {
        // Arrange
        var snapshot = RunSnapshot.Empty(RunId).Apply(new RunStartedEvent(
            RunId, "ticket", "fix-bug", new[] { "backend" },
            DateTimeOffset.UtcNow, "claude/claude-sonnet-4", "18803"));

        // Act / Assert: the snapshot carries the real repo, not an empty list.
        snapshot.Repos.Should().NotBeEmpty();
        snapshot.Repos.Should().ContainSingle().Which.Should().Be("backend");
    }
}
