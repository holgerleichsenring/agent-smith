using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TicketActs;

/// <summary>2026-11-17-e061c: the 'comment' run's step posts each comment, reports each, and nudges only when asked.</summary>
public sealed class PostTicketCommentsHandlerTests
{
    private readonly Mock<ITicketProvider> _tracker = new();
    private readonly Mock<ITicketActivityNudge> _activity = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostTicketComments_OneFails_TheNextIsStillPosted_AndPostedOnesAreNudgedWhenAsked(bool nudge)
    {
        _tracker.Setup(t => t.UpdateStatusAsync(new TicketId("1"), "a", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("down\nmore"));
        var (pipeline, slot) = (new PipelineContext(), new ActSlot<IReadOnlyList<TicketCommentPosted>>());
        pipeline.Set(ContextKeys.PostTicketCommentsRequest, new PostTicketCommentsRequest([new("1", "a"), new("2", "b")], nudge));
        pipeline.Set(ContextKeys.ActOutcome, slot);

        var result = await Handler().ExecuteAsync(new PostTicketCommentsContext(pipeline, Project), default);

        result.IsSuccess.Should().BeTrue();
        slot.Value!.Should().Equal(new TicketCommentPosted("1", "down"), new TicketCommentPosted("2", null));
        _tracker.Verify(t => t.UpdateStatusAsync(new TicketId("2"), "b", It.IsAny<CancellationToken>()), Times.Once);
        _activity.Verify(a => a.ChangedAsync("alpha", "github-2", It.IsAny<CancellationToken>()), nudge ? Times.Once() : Times.Never());
        _activity.Verify(a => a.ChangedAsync("alpha", "github-1", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PostTicketComments_NoRequestSeeded_FailsNamingTheKey()
    {
        var result = await Handler().ExecuteAsync(new PostTicketCommentsContext(new PipelineContext(), Project), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(nameof(ContextKeys.PostTicketCommentsRequest));
    }

    private static readonly ResolvedProject Project = new()
    {
        Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
    };

    private PostTicketCommentsHandler Handler()
    {
        var trackers = new Mock<ITicketProviderFactory>();
        trackers.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_tracker.Object);
        return new PostTicketCommentsHandler(trackers.Object, _activity.Object, NullLogger<PostTicketCommentsHandler>.Instance);
    }
}
