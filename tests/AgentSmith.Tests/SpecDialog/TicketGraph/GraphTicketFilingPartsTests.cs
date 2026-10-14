using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.SpecDialog.TicketGraph.TicketGraphRig;

namespace AgentSmith.Tests.SpecDialog.TicketGraph;

/// <summary>2026-10-14-dbcel: the parts of a list's filing — the ticket body, the ticket-text approval record, the
/// shared labels and the start pass's wording.</summary>
public sealed class GraphTicketFilingPartsTests
{
    [Fact]
    public void GraphTicketBody_Render_RequirementCriteriaRequiresAndConversation()
    {
        var content = GraphTicketBody.Render(Ticket("ui", "api"), [("#101", "Title api"), ("#41", null)], "conv-9", "LABEL NOTE\n");

        content.Title.Should().Be("Title ui");
        content.Body.Should().Be(
            "Requirement of ui.\n\n"
            + "## Acceptance criteria\n- ui works\n- ui is tested\n\n"
            + "## Requires\nMerge these first; this ticket starts once each is merged or marked done by hand.\n"
            + "- #101 — Title api\n- #41\n\n"
            + "Filed from design conversation `conv-9`; its specs are derived by the run that starts it.\n\n"
            + "LABEL NOTE\n");
    }

    [Fact]
    public void GraphTicketBody_NoRequires_OmitsTheSection()
    {
        GraphTicketBody.Render(Ticket("api"), [], "conv-9", null).Body.Should().NotContain("## Requires");
    }

    [Fact]
    public void GraphTicketBody_DoneItems_ReadBackByAcceptanceCriteriaSection()
    {
        var ticket = Ticket("ui", "api") with { Done = ["the page\nloads", "no file outside .agentsmith/ contains X"] };
        var stamp = FiledTicketLabels.ApprovedSetStamp;

        var body = GraphTicketBody.Render(ticket, [("#101", "Title api")], "conv-9", TicketLabelNote.For([stamp], stamp)).Body;

        AcceptanceCriteriaSection.Read(body).Should().Equal("the page loads", "no file outside .agentsmith/ contains X");
    }

    [Fact]
    public async Task GraphTicketApproval_Record_CitesEverySetAndImageWithTheProjectAndTicketText()
    {
        var store = new InMemorySpecApprovalStore();
        var references = new HeldMaterial(["set-a", "set-b"], ["img-1"]);
        var rig = new TicketGraphRig();
        var approval = new GraphTicketApproval(store, references, new SeriesRepos(), TimeProvider.System, NullLogger<GraphTicketApproval>.Instance);

        var record = await approval.RecordAsync(State(), rig.Project, "101", Ticket("api") with { Repos = ["sample-api"] }, CancellationToken.None);

        record.Should().BeEquivalentTo(new
        {
            Key = rig.KeyOf("101"), Tracker = "sample-tracker", TicketId = "101", Project = "proj", TicketText = true,
            Repositories = new[] { "sample-api" }, CarryingRepo = "sample-api",
            CitedSets = new[] { "set-a", "set-b" }, CitedImages = new[] { "img-1" },
        });
        record.Set.Specs.Should().BeEmpty();
        record.Set.Goal.Should().Be("Title api");
        record.Approval!.Conversation.Should().Be(ConversationId);
        (await store.GetAsync("sample-tracker", rig.KeyOf("101"), CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task GraphTicketApproval_TicketWithoutRepos_TakesTheConversationsRepositories()
    {
        var rig = new TicketGraphRig();
        var approval = new GraphTicketApproval(new InMemorySpecApprovalStore(), new HeldMaterial([], []), new SeriesRepos(),
            TimeProvider.System, NullLogger<GraphTicketApproval>.Instance);

        var record = await approval.RecordAsync(State(), rig.Project, "101", Ticket("api"), CancellationToken.None);

        record.Repositories.Should().Equal("sample-api", "sample-web");
        record.CarryingRepo.Should().BeEmpty("two repositories leave the carrier to the run");
    }

    [Theory]
    [InlineData(TrackerType.AzureDevOps)]
    [InlineData(TrackerType.Jira)]
    public void ApprovedSetTicketFiler_Labels_SameStampTheSpecFilingWrites(TrackerType type)
    {
        var project = new ResolvedProject { Name = "proj", Tracker = new TrackerConnection { Name = "t", Type = type } };

        ApprovedSetTicketFiler.Labels(project).Should().Equal(TicketLabelVocabulary.For(project.Tracker).ApprovedSetStamp);
    }

    [Fact]
    public async Task TicketGraphStarts_HeldReasonOnJira_NamesTheJiraKeys()
    {
        var rig = new TicketGraphRig(TrackerType.Jira);
        rig.Tracker.Existing.Add("PROJ-7");

        var report = await rig.FileAsync(List(Ticket("api", "ticket:PROJ-7"), Ticket("ui", "api")));

        report.Filed.Select(f => f.Start!.Reason).Should().Equal(
            "waits for PROJ-7 to be merged or marked done by hand",
            "waits for PROJ-101 to be merged or marked done by hand");
    }

    [Fact]
    public async Task TicketGraphStarts_IncompleteTicket_IsNotStartedAndNeverTagged()
    {
        var rig = new TicketGraphRig();
        List<FiledTicket> filed = [new("https://tracker.test/101", "t") { TicketId = "101" }];

        await rig.Starts().SettleAsync(rig.Tracker, rig.Project,
            [new CreatedGraphTicket(new CreatedTicket(new TicketId("101"), null), [], Complete: false)], ["stamp"], true, filed);

        filed[0].Start.Should().Be(new FiledWorkStart(FiledStartState.NotStarted, "created but not stored — do not trigger it; close it"));
        rig.Tracker.Labels.Should().BeEmpty();
        rig.Tracker.Moves.Should().BeEmpty();
    }

    [Fact]
    public void FiledWorkStart_Held_SerializedByNameAndLabelled()
    {
        var start = new FiledWorkStart(FiledStartState.Held, "waits for #101 to be merged or marked done by hand");

        System.Text.Json.JsonSerializer.Serialize(start).Should().Contain("\"Held\"");
        start.Note.Should().Be("\n  - held: waits for #101 to be merged or marked done by hand");
    }

    private sealed class HeldMaterial(IReadOnlyList<string> sets, IReadOnlyList<string> images) : IReferenceSetReader
    {
        public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReferenceSetFile>>([]);

        public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult(sets);

        public Task<IReadOnlyList<string>> ImageSetIdsAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult(images);

        public Task<ReferenceImageFile?> ImageAsync(string sessionId, string setId, CancellationToken cancellationToken) =>
            Task.FromResult<ReferenceImageFile?>(null);
    }
}
