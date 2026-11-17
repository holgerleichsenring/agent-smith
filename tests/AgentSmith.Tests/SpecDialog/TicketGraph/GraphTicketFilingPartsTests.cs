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
using AgentSmith.Application.Services.Filing;

namespace AgentSmith.Tests.SpecDialog.TicketGraph;

/// <summary>2026-10-14-dbcel: the parts of a list's filing — the ticket body, the approval record (2026-11-17-5701b: the
/// ticket's own specs), the shared labels and the start pass's wording.</summary>
public sealed class GraphTicketFilingPartsTests
{
    // 2026-11-17-dd05b: a list ticket's body is its specs' region, then what must merge first — never its free text.
    private static readonly SpecTicketRenderer Renderer = new();

    private static SpecTicketContent Region(params SpecDraft[] specs) =>
        new SeriesTicketRegion(Renderer).Render("Title ui", "2026-11-17-aaaa", specs, "conv-9", "LABEL NOTE\n");

    [Fact]
    public void GraphTicketBody_Render_SpecRegionThenRequires()
    {
        var content = GraphTicketBody.Render(Ticket("ui", "api"), Region(SpecOf("ui") with { Done = ["ui works"] }),
            [("#101", "Title api"), ("#41", null)]);

        content.Title.Should().Be("Title ui");
        content.Body.Should().StartWith(FramedTicketRegion.Begin).And.Contain("Goal of ui").And.Contain("- ui works")
            .And.Contain("Approved in design conversation `conv-9`.").And.NotContain("Requirement of ui.");
        content.Body.Should().EndWith("## Requires\nMerge these first; this ticket starts once each is merged or marked done by hand.\n"
            + "- #101 — Title api\n- #41\n");
    }

    [Fact]
    public void GraphTicketBody_NoRequires_OmitsTheSection()
    {
        GraphTicketBody.Render(Ticket("api"), Region(SpecOf("api")), []).Body.Should().NotContain("## Requires");
    }

    [Fact]
    public void SeriesTicketRegion_SeveralSpecs_ListsEverySliceUnderTheGoal()
    {
        var body = Region(SpecOf("ui"), SpecOf("api")).Body;

        body.Should().StartWith(FramedTicketRegion.Begin).And.Contain("Title ui").And.Contain("## Slices")
            .And.Contain("`ui-spec` Goal of ui").And.Contain("`api-spec` Goal of api");
    }

    [Fact]
    public void SeriesTicketRegion_OneSpec_IsTheFiledSpecBody()
    {
        Region(SpecOf("ui")).Body.Should().Be(Renderer.RenderSpec(SpecOf("ui"), "conv-9", "LABEL NOTE\n").Body);
    }

    [Fact]
    public async Task ListTicketRecord_Filed_CitesEverySetAndImageWithItsOwnSpecsAndRepos()
    {
        var rig = new TicketGraphRig { References = new HeldMaterial(["set-a", "set-b"], ["img-1"]) };

        await rig.FileAsync(List(Ticket("api") with { Repos = ["sample-api"] }));

        var record = (await rig.Approvals.GetAsync("sample-tracker", rig.KeyOf("101"), CancellationToken.None))!;
        record.Should().BeEquivalentTo(new
        {
            Key = rig.KeyOf("101"), Tracker = "sample-tracker", TicketId = "101", Project = "proj", TicketText = false,
            Repositories = new[] { "sample-api" }, CarryingRepo = "sample-api",
            CitedSets = new[] { "set-a", "set-b" }, CitedImages = new[] { "img-1" },
        });
        record.Set.Specs.Should().ContainSingle().Which.Draft.Goal.Should().Be("Goal of api");
        record.Set.Goal.Should().Be("Title api");
        record.Approval!.Conversation.Should().Be(ConversationId);
    }

    [Fact]
    public async Task ListTicketRecord_TicketWithoutRepos_TakesTheConversationsRepositories()
    {
        var rig = new TicketGraphRig { References = new HeldMaterial([], []) };

        await rig.FileAsync(List(Ticket("api")));

        var record = (await rig.Approvals.GetAsync("sample-tracker", rig.KeyOf("101"), CancellationToken.None))!;
        record.Repositories.Should().Equal("sample-api", "sample-web");
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
            [new CreatedGraphTicket(new CreatedTicket(new TicketId("101"), null), [], Complete: false, ["stamp"])], true, filed);

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
