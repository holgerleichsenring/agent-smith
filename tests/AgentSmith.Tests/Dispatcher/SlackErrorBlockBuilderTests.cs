using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services.Adapters;
using FluentAssertions;

namespace AgentSmith.Tests.Dispatcher;

public sealed class SlackErrorBlockBuilderTests
{
    [Fact]
    public void Build_ReturnsNonEmptyFallbackText()
    {
        var ctx = CreateErrorContext();

        var (fallbackText, _) = SlackErrorBlockBuilder.Build(ctx);

        fallbackText.Should().NotBeNullOrWhiteSpace();
        fallbackText.Should().Contain("42");
    }

    [Fact]
    public void Build_ReturnsBlockKitBlocks()
    {
        var ctx = CreateErrorContext();

        var (_, blocks) = SlackErrorBlockBuilder.Build(ctx);

        blocks.Should().NotBeNull();
        blocks.Should().NotBeEmpty();
    }

    [Fact]
    public void Build_IncludesRetryButton()
    {
        var ctx = CreateErrorContext();

        var (_, blocks) = SlackErrorBlockBuilder.Build(ctx);

        var json = System.Text.Json.JsonSerializer.Serialize(blocks);
        json.Should().Contain("Retry");
    }

    private static ErrorContext CreateErrorContext() => new(
        JobId: "job-123",
        ChannelId: "C12345",
        TicketId: 42,
        Project: "my-project",
        FailedStep: 3,
        TotalSteps: 9,
        StepName: "Generating plan",
        RawError: "API connection failed",
        FriendlyError: "Could not reach a required service");
}
