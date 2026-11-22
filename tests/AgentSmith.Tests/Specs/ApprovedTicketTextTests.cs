using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-22-a720b: a revision a newer approval caused rewrites the ticket's text from its specs before the branch is
/// written, and the set carries the fingerprint of the ticket as the tracker stored it, label note stripped.
/// </summary>
public sealed class ApprovedTicketTextTests
{
    private static readonly DateTimeOffset T = new(2026, 11, 22, 18, 0, 0, TimeSpan.Zero);
    private static readonly TrackerConnection Tracker = new();
    private readonly Mock<ITicketRewriter> _rewriter = new();
    private readonly Mock<ITicketProvider> _tracker = new();
    private readonly Mock<ITicketProviderFactory> _factory = new();
    private readonly Mock<AgentSmith.Contracts.Events.IEventPublisher> _events = new();

    public ApprovedTicketTextTests()
    {
        _factory.Setup(f => f.CreateRewriter(It.IsAny<TrackerConnection>())).Returns(_rewriter.Object);
        _factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_tracker.Object);
        _rewriter.Setup(r => r.RewriteRegionAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<RegionPlacement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketRewriteResult.Ok);
        _tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Stored());
    }

    [Fact]
    public async Task FollowAsync_NewerApproval_RewritesTheRegionAndCarriesTheStrippedReadBackFingerprint()
    {
        var pipeline = Pipeline();

        var followed = await Text().FollowAsync(pipeline, Tracker, Set(T.AddHours(1)), Set(T), CancellationToken.None);

        _rewriter.Verify(r => r.RewriteRegionAsync(new TicketId("42"), It.Is<string>(s => s.Contains("Goal two")),
            It.IsAny<RegionPlacement>(), It.IsAny<CancellationToken>()), Times.Once);
        followed.TicketFingerprint.Should().Be(TicketTextFingerprint.Of(TicketLabelNoteStripper.Strip(Stored())),
            "the next run fetches the ticket with the label note stripped and must read no edit");
        pipeline.Get<Ticket>(ContextKeys.Ticket).Description.Should().NotContain(TicketLabelNote.BeginIdentifier);
        _events.Verify(e => e.PublishAsync(It.Is<AgentSmith.Contracts.Events.TicketTextRewrittenEvent>(r =>
            r.RunId == "run-1" && r.TicketFingerprint == followed.TicketFingerprint), It.IsAny<CancellationToken>()), Times.Once,
            "the attempt keeps the text it left behind (2026-11-22-e812)");
    }

    [Fact]
    public async Task FollowAsync_SameApproval_LeavesTheTicketAndTheSetAlone()
    {
        var set = Set(T);

        var followed = await Text().FollowAsync(Pipeline(), Tracker, set, Set(T), CancellationToken.None);

        followed.Should().BeSameAs(set, "a revision the approval did not cause — a kept edit, a rework — never rewrites");
        _rewriter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FollowAsync_DerivedSetPreviewOrFirstHandoff_LeavesTheTicketAlone()
    {
        var preview = Pipeline();
        preview.Set(ContextKeys.SpecDerivationPreview, true);

        await Text().FollowAsync(Pipeline(), Tracker, Set(T.AddHours(1)) with { Approval = null }, Set(T), CancellationToken.None);
        await Text().FollowAsync(preview, Tracker, Set(T.AddHours(1)), Set(T), CancellationToken.None);
        await Text().FollowAsync(Pipeline(), Tracker, Set(T.AddHours(1)), null, CancellationToken.None);

        _rewriter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FollowAsync_TextAPersonEditedSinceTheLastWrite_IsKeptAsAComment_NeverSilently()
    {
        await Text().FollowAsync(Pipeline(), Tracker, Set(T.AddHours(1)), Set(T), CancellationToken.None);

        _tracker.Verify(t => t.UpdateStatusAsync(new TicketId("42"),
            It.Is<string>(b => b.StartsWith(ApprovedTicketText.ReplacedHeading) && b.Contains("Old text")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FollowAsync_TextAsAgentSmithWroteIt_IsRewrittenWithoutAComment()
    {
        var previous = Set(T) with { TicketFingerprint = TicketTextFingerprint.Of(Pipeline().Get<Ticket>(ContextKeys.Ticket)) };

        await Text().FollowAsync(Pipeline(), Tracker, Set(T.AddHours(1)), previous, CancellationToken.None);

        _tracker.Verify(t => t.UpdateStatusAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FollowAsync_ReCutDemand_LeavesTheTicketAlone()
    {
        var demanded = Set(T.AddHours(1)) with { Revisions = [new SpecRevision(2, SpecRevisionCause.RecutDemand, T)] };

        await Text().FollowAsync(Pipeline(), Tracker, demanded, Set(T), CancellationToken.None);

        _rewriter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FollowAsync_RefusedRewrite_PublishesTheSetAsItWas()
    {
        _rewriter.Setup(r => r.RewriteRegionAsync(It.IsAny<TicketId>(), It.IsAny<string>(), It.IsAny<RegionPlacement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TicketRewriteResult.Failed("the tracker refused"));
        var set = Set(T.AddHours(1));

        var followed = await Text().FollowAsync(Pipeline(), Tracker, set, Set(T), CancellationToken.None);

        followed.Should().BeSameAs(set);
    }

    private ApprovedTicketText Text() => ApprovedSetDoubles.TicketText(_factory.Object, _events.Object);

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Ticket, new Ticket(new TicketId("42"), "Widget", "Old text", null, "Open", "GitHub"));
        pipeline.Set(ContextKeys.RunId, "run-1");
        return pipeline;
    }

    private static Ticket Stored() => new(new TicketId("42"), "Widget",
        $"{TicketLabelNote.Begin} note {TicketLabelNote.End}\nGoal one\nGoal two", null, "Open", "GitHub");

    private static SpecSet Set(DateTimeOffset approvedAt) => new(
        "github:42",
        [Spec("2026-11-22-297aa", "Goal one"), Spec("2026-11-22-297ab", "Goal two")],
        SpecAccounting.Empty, [new SpecRevision(1, $"{SpecRevisionCause.Approval} session-1", T)], SpecSource.Approved,
        TicketFingerprint: "old", Approval: new SpecApproval(approvedAt, "session-1", "carol"), Series: "2026-11-22-297a");

    private static DerivedSpec Spec(string id, string goal) =>
        new(new SpecDraft(id, goal, $"spec: {id}", []) { Done = [$"{goal} holds."] }, id, string.Empty, []);
}
