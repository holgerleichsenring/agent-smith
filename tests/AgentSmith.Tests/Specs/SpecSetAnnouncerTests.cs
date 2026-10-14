using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-10-13-9a59d: a placed set is announced whether its write committed or not.</summary>
public sealed class SpecSetAnnouncerTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SpecSetAnnouncer_WriteFailed_StillPublishesWithCommittedFalse()
    {
        var events = new Mock<IEventPublisher>();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-2");
        var set = new SpecSet("github-42", [], new SpecAccounting([], [], []),
            [new SpecRevision(1, "initial", At), new SpecRevision(2, "ticket edited", At.AddHours(1))],
            SpecSource.BranchArtifact, ExecutedSpecIds: ["2026-10-13-1234a"],
            Approval: new SpecApproval(At.AddMinutes(30), "session-7", "carol"), Series: "2026-10-13-1234");

        await new SpecSetAnnouncer(events.Object, NullLogger<SpecSetAnnouncer>.Instance)
            .AnnounceAsync(pipeline, set, committed: false, CancellationToken.None);

        events.Verify(e => e.PublishAsync(It.Is<SpecSetPublishedEvent>(p =>
            p.RunId == "run-2" && !p.Committed && p.RevisionNumber == 2 && p.RevisionCause == "ticket edited"
            && p.SeriesId == "2026-10-13-1234" && p.CarriedSpecIds!.SequenceEqual(new[] { "2026-10-13-1234a" })
            && p.ApprovalAt == At.AddMinutes(30) && p.ApprovalConversation == "session-7" && p.ApprovalPrincipal == "carol"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
