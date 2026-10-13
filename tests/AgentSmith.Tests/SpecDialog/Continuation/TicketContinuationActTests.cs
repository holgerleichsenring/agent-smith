using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: approving in a ticket's own conversation continues that ticket — recorded under it for
/// its next attempt, never filed anew — and the preview says beforehand exactly what the approval records.
/// </summary>
public sealed class TicketContinuationActTests : IDisposable
{
    private const string Series = "2026-10-13-ab12";
    private static readonly string A = Series + "a", B = Series + "b", C = Series + "c";

    private readonly ContinuationHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task BoundApproval_WorkedTicket_RecordsContinuation_FilesNothing_ClearsProposal()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A]);
        var proposal = Lone(Draft(B, "B, re-cut"));
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal);

        _h.Tracker.Created.Should().BeEmpty("an open ticket is continued, never filed anew");
        var record = (await _h.RecordAsync())!;
        record.Continuation!.BasisRunId.Should().Be("r-1");
        record.Continuation.Dispositions.Select(d => (d.SpecId, d.Kind)).Should().Equal(
            (B, SpecDispositionKinds.Recut), (A, SpecDispositionKinds.Kept));
        record.Set.Specs.Should().ContainSingle().Which.Draft.Goal.Should().Be("B, re-cut");
        _h.Sources.Writes.Should().BeEmpty("the run applies the dispositions to the branch, not the server");
        (await _h.OpenProposalJsonAsync()).Should().BeNull("an acted-on proposal is not offered again");
    }

    [Fact]
    public async Task BoundApproval_NeverWorked_RecordsSeriesAndBranch_RewritesWhereSupported()
    {
        var proposal = Lone(Draft("p-1", "the widget stops dropping"));
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal);

        var record = (await _h.RecordAsync())!;
        record.Continuation.Should().BeNull("a never-worked hand-off continues nothing");
        record.Set.Specs.Should().ContainSingle().Which.Draft.Goal.Should().Be("the widget stops dropping");
        _h.Sources.Writes.Should().ContainSingle();
        _h.Tracker.Regions.Should().ContainSingle();
        _h.Tracker.Created.Should().BeEmpty();
        (await _h.OpenProposalJsonAsync()).Should().BeNull();
    }

    [Fact]
    public async Task BoundApproval_TicketWithOnlyPrReviewRuns_IsNeverWorked()
    {
        await _h.AddAsync(TicketStoreHarness.Run("r-pr", pipeline: "pr-review"), TicketStoreHarness.Step("r-pr"),
            TicketStoreHarness.Run("r-scan", pipeline: "security-scan"), TicketStoreHarness.Step("r-scan"));
        await _h.BindAsync(null);

        var outcome = await _h.ActAsync(Lone(Draft("p-1", "the widget stops dropping")));

        outcome.Acted.Should().BeTrue();
        (await _h.RecordAsync())!.Continuation.Should().BeNull();
        _h.Sources.Writes.Should().ContainSingle("a never-worked ticket takes the approved series whole");
    }

    [Fact]
    public async Task BoundApproval_BugOutcome_StillFilesBugTicket()
    {
        var bug = new BugOutcome(new BugTicketDraft("Widget drops", "It drops.", "It stops."));
        await _h.BindAsync(bug);

        await _h.AcceptAsync(bug);

        _h.Tracker.Created.Should().ContainSingle();
        (await _h.RecordAsync()).Should().BeNull();
    }

    [Fact]
    public async Task BoundApproval_TicketWorkedBySiblingProject_IsWorked()
    {
        await _h.WorkedAsync("r-beta", "beta", Series, [A, B], executed: [A]);
        await _h.BindAsync(null);

        var outcome = await _h.ActAsync(Lone(Draft(B, "B, re-cut")));

        outcome.Acted.Should().BeTrue();
        (await _h.RecordAsync())!.Continuation!.BasisRunId.Should().Be("r-beta");
        _h.Sources.Writes.Should().BeEmpty("a revision-1 branch write over the sibling's series would erase it");
    }

    [Fact]
    public async Task BoundApproval_TicketFetchFails_RefusedWithReason_NothingRecorded_ProposalKept()
    {
        var proposal = Lone(Draft(B, "B, re-cut"));
        await _h.BindAsync(proposal);
        _h.Tracker.ReadFails = new HttpRequestException("the tracker is down");

        await _h.AcceptAsync(proposal);

        (await _h.RecordAsync()).Should().BeNull();
        (await _h.OpenProposalJsonAsync()).Should().NotBeNull("the person approves again once the tracker answers");
        (await TranscriptAsync()).Should().Contain("the tracker is down").And.Contain("Nothing was recorded");
    }

    [Fact]
    public async Task BoundApproval_ContinuationRecord_RepositoriesAreBasisUnionProposal_CarrierIsBasis()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], carrier: "web");
        await _h.BindAsync(null);

        await _h.ActAsync(Lone(Draft(B, "B, re-cut")));

        var record = (await _h.RecordAsync())!;
        record.Repositories.Should().Equal("web", "api");
        record.CarryingRepo.Should().Be("web");
    }

    [Fact]
    public async Task ApprovedRepoScope_ContinuationRecord_KeepsExecutedSpecsRepositories()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], carrier: "web");
        await _h.BindAsync(null);
        await _h.ActAsync(Lone(Draft(B, "B, re-cut")));
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TrackerPlatform, "github");
        pipeline.Set(ContextKeys.TrackerConnection, "gh");
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "api" }, new RepoConnection { Name = "web" }, new RepoConnection { Name = "docs" }]);

        var scope = await ApprovedSetDoubles.Scope(_h.Approvals).OpenAsync(
            pipeline, new Ticket(new TicketId(ContinuationHarness.TicketId), "t", "d", null, "Open", "github"), default);

        scope.Repos.Select(r => r.Name).Should().Equal("api", "web");
    }

    [Fact]
    public async Task BoundApproval_OverMaxSpecs_RefusedNamingBothCounts_ProposalKept()
    {
        var ids = Enumerable.Range(0, 8).Select(i => $"{Series}{(char)('a' + i)}").ToList();
        await _h.WorkedAsync("r-1", "alpha", Series, ids, executed: ids.Take(6).ToList());
        var proposal = Lone(Draft("new-one", "One more"));
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal);

        (await _h.RecordAsync()).Should().BeNull();
        (await _h.OpenProposalJsonAsync()).Should().NotBeNull();
        (await TranscriptAsync()).Should().Contain("6 executed").And.Contain("3 unstarted");
    }

    [Fact]
    public async Task BoundApproval_Stored_PublishesTicketActivityChanged()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A]);
        await _h.BindAsync(null);

        await _h.ActAsync(Lone(Draft(B, "B, re-cut")));

        _h.Activity.Verify(a => a.ChangedAsync("alpha", ContinuationHarness.Key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Decide_TicketInOwnDoneStatus_IsNotClosedForWork_Continues()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A, B]);
        (_h.Tracker.Status, _h.Tracker.IsClosed) = ("In Review", true);
        await _h.BindAsync(null);

        var outcome = await _h.ActAsync(Lone(Draft("new-one", "Follow-on work")));

        outcome.Acted.Should().BeTrue();
        _h.Tracker.Created.Should().BeEmpty("a ticket agent-smith moved to done is continued, not followed up");
        (await _h.RecordAsync())!.Continuation!.Dispositions.Should().Contain(new SpecDisposition(C, SpecDispositionKinds.Appended));
    }

    [Fact]
    public async Task ClosedTicket_Approval_FilesFollowUp_WithoutExecutedSpecs_CommentsOnBoth()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A]);
        (_h.Tracker.Status, _h.Tracker.IsClosed) = ("Closed", true);
        var proposal = new EpicOutcome(Draft("parent", "The widget is reliable"),
            [Draft(A, "A rewritten after it ran"), Draft("new-one", "Report every drop")]);
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal);

        _h.Tracker.Created.Should().ContainSingle().Which.Should().Contain("Report every drop")
            .And.NotContain("A rewritten after it ran");
        _h.Tracker.Comments.Should().Contain(c => c.TicketId == "42" && c.Comment.Contains("#43"))
            .And.Contain(c => c.TicketId == "43" && c.Comment.Contains("#42"));
        (await _h.OpenProposalJsonAsync()).Should().BeNull();
    }

    [Fact]
    public async Task ApprovalPreview_TicketFetchFails_502WithReason()
    {
        await _h.BindAsync(Lone(Draft(B, "B, re-cut")));
        _h.Tracker.ReadFails = new HttpRequestException("the tracker is down");

        var result = await _h.InScopeAsync(sp => TicketApprovalPreviewEndpoints.PreviewAsync(
            "alpha", "42", ContinuationHarness.Http(sp), sp.GetRequiredService<TicketApprovalPreview>(), default));

        var json = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        json.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
        System.Text.Json.JsonSerializer.Serialize(((IValueHttpResult)result).Value).Should().Contain("the tracker is down");
    }

    [Fact]
    public async Task ApprovalPreview_MatchesWhatTheApprovalRecords()
    {
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B, C], executed: [A]);
        var proposal = new EpicOutcome(Draft("parent", "The widget is reliable"),
            [Draft(B, "B, re-cut"), Draft("new-one", "Report every drop")]);
        await _h.BindAsync(proposal);

        var preview = (await _h.PreviewAsync())!.View!;
        await _h.AcceptAsync(proposal);

        preview.Effect.Should().Be(TicketContinuationEffects.Continue);
        preview.Specs.Select(s => new SpecDisposition(s.SpecId, s.Disposition)).Should().Equal(
            (await _h.RecordAsync())!.Continuation!.Dispositions);
        preview.Specs.Select(s => s.Disposition).Should().Equal(
            SpecDispositionKinds.Recut, SpecDispositionKinds.Appended, SpecDispositionKinds.Kept, SpecDispositionKinds.Dropped);
    }

    [Fact]
    public async Task ApprovalPreview_NoConversationOrNoOpenProposal_404()
    {
        var none = await _h.InScopeAsync(sp => TicketApprovalPreviewEndpoints.PreviewAsync(
            "alpha", "42", ContinuationHarness.Http(sp), sp.GetRequiredService<TicketApprovalPreview>(), default));
        await _h.BindAsync(null);
        var noProposal = await _h.InScopeAsync(sp => TicketApprovalPreviewEndpoints.PreviewAsync(
            "alpha", "42", ContinuationHarness.Http(sp), sp.GetRequiredService<TicketApprovalPreview>(), default));

        none.Should().BeOfType<NotFound>();
        noProposal.Should().BeOfType<NotFound>();
    }

    private Task<string> TranscriptAsync() => _h.InScopeAsync(async sp =>
        (await sp.GetRequiredService<AgentSmith.Infrastructure.Persistence.Repositories.SpecDialogSessionRepository>()
            .GetBySessionIdAsync(ContinuationHarness.SessionId, default))!.TranscriptJson);

    private static SpecOutcome Lone(SpecDraft draft) => new(draft);

    private static SpecDraft Draft(string id, string goal) =>
        new(id, goal, $"spec: {id}\ngoal: \"{goal}\"\ndone:\n  - \"{goal} works.\"\n", []) { Repos = ["api"], Done = [$"{goal} works."] };
}
