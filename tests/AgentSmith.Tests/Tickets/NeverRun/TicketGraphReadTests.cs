using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using static AgentSmith.Tests.Tickets.NeverRun.NeverRunFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.NeverRun;

/// <summary>
/// 2026-10-14-dbcen: the requires graph and one ticket's requires — every ticket on either end of a stored edge,
/// its state computed per read from the list's own summaries and the landings, By shown as stored.
/// </summary>
public sealed class TicketGraphReadTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    private TicketGraphReader Graph => _h.Services.GetRequiredService<TicketGraphReader>();
    private TicketRequiresReader Requires => _h.Services.GetRequiredService<TicketRequiresReader>();

    private async Task<TicketGraphNodeView> NodeAsync(string id, string? project = "alpha") =>
        (await Graph.ReadAsync(project, default)).Nodes.Single(n => n.TicketId == id);

    [Fact]
    public async Task TicketGraph_TwoTicketsOneEdge_ReturnsBothNodesAndTheEdgeFromTheDependent()
    {
        await _h.AddAsync(Filing("58", "Orders: API"), Filing("59", "Orders: list screen"));
        await _h.RequiresAsync("59", "58");

        var graph = await Graph.ReadAsync("alpha", default);

        graph.Nodes.Select(n => (n.TicketId, n.Key, n.Title)).Should().BeEquivalentTo(
            [("59", "#59", "Orders: list screen"), ("58", "#58", "Orders: API")]);
        graph.Edges.Should().Equal(new TicketGraphEdgeView("gh:github-59", "gh:github-58"));
        graph.Nodes.Select(n => n.Id).Should().Contain(["gh:github-59", "gh:github-58"]);
    }

    [Fact]
    public async Task TicketGraph_StatePrecedence_LandingOverRunningOverBlockedOverReady()
    {
        await _h.AddAsync(Filing("58"), Filing("59"), Filing("60"), Filing("61"), Run("r-1", "61", status: "running"));
        await _h.RequiresAsync("59", "58");
        await _h.RequiresAsync("60", "59");
        await _h.RequiresAsync("61", "58");
        await _h.LandAsync("57", TicketLandingKinds.Merged);
        await _h.AddAsync(Filing("57"));
        await _h.RequiresAsync("62", "57");
        await _h.AddAsync(Filing("62"), Run("r-2", "57", status: "running"));

        var states = (await Graph.ReadAsync("alpha", default)).Nodes.ToDictionary(n => n.TicketId, n => n.State);

        states["57"].Should().Be("merged", "a landing outranks a running attempt");
        states["61"].Should().Be("running", "a running attempt outranks an unmet requirement");
        states["60"].Should().Be(TicketBucket.Blocked);
        states["59"].Should().Be(TicketBucket.Blocked);
        states["62"].Should().Be("ready", "what it requires landed");
        states["58"].Should().Be("ready");
    }

    [Fact]
    public async Task TicketGraph_RunBearingNode_HasTheListRowsBucketIncludingStuck()
    {
        var text = new Ticket(new TicketId("58"), "Ticket 58", "Export it.", "In a second.", "Open", "github");
        var run = Run("r-1", "58", status: "failed");
        run.ActsReadAt = T;
        await _h.AddAsync(run, Step("r-1"),
            new RunAttempt { RunId = "r-1", TicketFingerprint = AgentSmith.Application.Services.Specs.TicketTextFingerprint.Of(text) },
            new TicketBetweenAttemptsCheck { RunId = "r-1", CheckedAt = T.AddHours(3), FingerprintUnchanged = true, ActsJson = "[]" },
            Filing("59"));
        await _h.RequiresAsync("59", "58");

        var node = await NodeAsync("58");
        var row = (await _h.ListAsync()).Tickets.Single(t => t.TicketId == "58");

        (node.State, node.Bucket, node.HasPage).Should().Be((TicketBucket.Stuck, TicketBucket.Stuck, true));
        node.Bucket.Should().Be(row.Bucket);
    }

    [Fact]
    public async Task TicketGraph_RequiredTicketWithoutRunOrFiling_IsIdleWithNoPage()
    {
        await _h.AddAsync(Filing("59"));
        await _h.RequiresAsync("59", "41");

        var node = await NodeAsync("41");

        (node.State, node.Bucket, node.HasPage, node.Title, node.Project).Should().Be((TicketBucket.Idle, TicketBucket.Idle, false, "#41", "alpha"));
    }

    [Fact]
    public async Task TicketGraph_NoProject_ReadsEveryConfiguredProject()
    {
        _h.Config.Projects["beta"] = new ResolvedProject
        {
            Name = "beta", Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
        };
        await _h.AddAsync(Filing("59"), Filing("70", project: "beta"));
        await _h.RequiresAsync("59", "58");
        await _h.Services.GetRequiredService<AgentSmith.Contracts.Services.ITicketRequirementStore>().AddAsync(
            [new TicketRequirementEdge(Tracker, KeyOf("70"), "70", "beta", KeyOf("71"), "71", true, default, "c")], default);

        var graph = await TicketGraphEndpoints.GraphAsync(null, Graph, default);

        var nodes = graph.Should().BeOfType<Ok<TicketGraphView>>().Which.Value!.Nodes;
        nodes.Select(n => n.TicketId).Should().BeEquivalentTo(["59", "58", "70", "71"]);
        nodes.Single(n => n.TicketId == "70").Should().Match<TicketGraphNodeView>(n => n.Project == "beta" && n.HasPage);
    }

    [Fact]
    public async Task TicketRequires_ReturnsRequiresAndRequiredByWithKeysAndLandingFacts()
    {
        await _h.AddAsync(Filing("58", "Orders: API"), Filing("59", "Orders: list screen"), Filing("60", "Orders: export"));
        await _h.RequiresAsync("59", "58", "41");
        await _h.RequiresAsync("60", "59");
        await _h.LandAsync("58", TicketLandingKinds.Merged, T.AddHours(1));

        var view = (await Requires.ReadAsync("alpha", "59", default))!;

        view.Requires.Select(r => (r.Key, r.Title, r.State, r.HasPage, r.LandedKind)).Should().Equal(
            ("#58", "Orders: API", "merged", true, TicketLandingKinds.Merged), ("#41", "#41", TicketBucket.Idle, false, null));
        view.Requires[0].LandedAt.Should().Be(T.AddHours(1));
        view.RequiredBy.Select(r => (r.TicketId, r.Key, r.State)).Should().Equal(("60", "#60", TicketBucket.Blocked));
    }

    [Fact]
    public async Task TicketRequires_LandedByHand_ShowsTheStoredByAsIs()
    {
        await _h.AddAsync(Filing("59"));
        await _h.RequiresAsync("59", "41");
        await _h.LandAsync("41");

        var entry = (await Requires.ReadAsync("alpha", "59", default))!.Requires.Single();

        (entry.State, entry.LandedBy, entry.Note).Should().Be(("done-by-hand", "Ada Lovelace", "shipped by hand"));
    }

    [Fact]
    public async Task TicketRequires_UnknownProject_Is404()
    {
        var result = await TicketGraphEndpoints.RequiresAsync("nowhere", "59", Requires, default);

        var text = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        text.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        result.Should().BeOfType<ContentHttpResult>().Which.ResponseContent.Should().Be("No project nowhere is configured.");
    }

    [Fact]
    public async Task TicketRequires_TicketWithoutRequires_IsEmpty()
    {
        var result = await TicketGraphEndpoints.RequiresAsync("alpha", "59", Requires, default);

        result.Should().BeOfType<Ok<TicketRequiresView>>().Which.Value!.Should()
            .Match<TicketRequiresView>(v => v.Requires.Count == 0 && v.RequiredBy.Count == 0);
    }
}
