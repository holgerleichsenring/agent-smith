using AgentSmith.Tests.Server.Auth;
using AgentSmith.Infrastructure.Persistence.Contracts;
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
/// 2026-10-14-dbcen: one ticket's requires — every ticket on either end of a stored edge,
/// its state computed per read from the list's own summaries and the landings, By shown as stored.
/// </summary>
public sealed class TicketRequiresReadTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    private TicketRequiresReader Requires => _h.Services.GetRequiredService<TicketRequiresReader>();

    // 2026-11-23-849e: the graph read is gone with the Tickets page's Graph view; the node state precedence it pinned is
    // the requires view's, pinned here.
    [Fact]
    public async Task TicketRequires_StatePrecedence_LandingOverRunningOverBlockedOverReady()
    {
        await _h.AddAsync(Filing("58"), Filing("59"), Filing("61"), Filing("63"), Run("r-1", "61", status: "running"));
        await _h.RequiresAsync("59", "58");
        await _h.RequiresAsync("63", "57", "61", "59", "58");
        await _h.LandAsync("57", TicketLandingKinds.Merged);
        await _h.AddAsync(Filing("57"), Run("r-2", "57", status: "running"));

        var states = (await Requires.ReadAsync("alpha", "63", default))!.Requires.ToDictionary(r => r.TicketId, r => r.State);

        states["57"].Should().Be("merged", "a landing outranks a running attempt");
        states["61"].Should().Be("running", "a running attempt outranks an unmet requirement");
        states["59"].Should().Be(TicketBucket.Blocked);
        states["58"].Should().Be("ready");
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
