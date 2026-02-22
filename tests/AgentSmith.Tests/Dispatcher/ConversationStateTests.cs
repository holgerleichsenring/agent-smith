using AgentSmith.Dispatcher.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Dispatcher;

public sealed class ConversationStateTests
{
    [Fact]
    public void WithPendingQuestion_SetsQuestionId()
    {
        var state = CreateState();

        var updated = state.WithPendingQuestion("q-123");

        updated.PendingQuestionId.Should().Be("q-123");
        updated.JobId.Should().Be(state.JobId);
    }

    [Fact]
    public void ClearPendingQuestion_RemovesQuestionId()
    {
        var state = CreateState().WithPendingQuestion("q-123");

        var cleared = state.ClearPendingQuestion();

        cleared.PendingQuestionId.Should().BeNull();
    }

    [Fact]
    public void Initializer_SetsAllProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new ConversationState
        {
            JobId = "job-1",
            ChannelId = "C1",
            UserId = "U1",
            Platform = "slack",
            Project = "backend",
            TicketId = 42,
            StartedAt = now
        };

        state.JobId.Should().Be("job-1");
        state.ChannelId.Should().Be("C1");
        state.Platform.Should().Be("slack");
        state.Project.Should().Be("backend");
        state.TicketId.Should().Be(42);
        state.StartedAt.Should().Be(now);
    }

    private static ConversationState CreateState() => new()
    {
        JobId = "job-abc",
        ChannelId = "C123",
        UserId = "U456",
        Platform = "slack",
        Project = "my-project",
        TicketId = 42,
        StartedAt = DateTimeOffset.UtcNow
    };
}
