using System.Net;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using static AgentSmith.Tests.Tickets.LandedByHand.LandedByHandFixture;

namespace AgentSmith.Tests.Tickets.LandedByHand;

/// <summary>
/// 2026-10-14-dbcei: a person marks a ticket done by hand with a note — stored once with who, when and why, after
/// the tracker said the ticket exists; every refusal a sentence, and nothing written on a 400, 404 or 503.
/// </summary>
public sealed class LandedByHandEndpointTests : IDisposable
{
    private readonly LandedByHandFixture _f = new();

    public void Dispose() => _f.Dispose();

    [Fact]
    public async Task LandedByHand_WithANote_StoresAByHandLandingWithWhoWhenAndWhy()
    {
        var landing = Landed(await _f.MarkAsync("  Shipped in the release branch  "));

        landing.Should().BeEquivalentTo(new TicketLandingFact(Tracker, "github-7", "7", "p", TicketLandingKinds.ByHand,
            T.AddHours(1), "Ada Lovelace", "Shipped in the release branch", null));
        var row = _f.Rows().Should().ContainSingle().Subject;
        (row.Kind, row.By, row.Note).Should().Be((TicketLandingKinds.ByHand, "Ada Lovelace", "Shipped in the release branch"));
    }

    [Fact]
    public async Task LandedByHand_TokenWithADisplayName_StoresTheNameNotTheSub() =>
        Landed(await _f.MarkAsync(user: Person(("sub", "u-9"), ("preferred_username", "grace@example.org"))))
            .By.Should().Be("grace@example.org");

    [Fact]
    public async Task LandedByHand_TokenWithoutDisplayClaims_StoresThePrincipal() =>
        Landed(await _f.MarkAsync(user: Person(("sub", "u-9")))).By.Should().Be("u-9");

    [Fact]
    public async Task LandedByHand_BlankNote_Is400WithTheSentence()
    {
        Said(await _f.MarkAsync("   ")).Should().Be((StatusCodes.Status400BadRequest, TicketLandedByHandEndpoints.NoteRequired));
        _f.Rows().Should().BeEmpty();
    }

    [Fact]
    public async Task LandedByHand_UnknownProject_Is404WithTheSentence()
    {
        Said(await _f.MarkAsync(project: "nope")).Should().Be((StatusCodes.Status404NotFound, "No project nope is configured."));
        _f.Rows().Should().BeEmpty();
    }

    [Fact]
    public async Task LandedByHand_TicketNotOnTheTracker_Is404AndWritesNothing()
    {
        _f.Tickets.ReadFails = new TicketNotFoundException(new TicketId("12"));

        Said(await _f.MarkAsync(ticket: "12")).Should().Be((StatusCodes.Status404NotFound, $"No ticket 12 on {Tracker}."));
        _f.Rows().Should().BeEmpty();
        _f.Events.Verify(e => e.PublishAsync(It.IsAny<SystemEvent>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task LandedByHand_TrackerUnreachable_Is503AndWritesNothing()
    {
        _f.Tickets.ReadFails = new HttpRequestException("host.internal refused", null, HttpStatusCode.BadGateway);

        Said(await _f.MarkAsync()).Should().Be((StatusCodes.Status503ServiceUnavailable,
            $"The tracker {Tracker} could not be read (unreachable); nothing was recorded."));
        _f.Rows().Should().BeEmpty();
    }

    [Fact]
    public async Task LandedByHand_TicketAlreadyMerged_Is409WithTheMergedLanding()
    {
        (await _f.MergedAsync("7")).Should().BeTrue();

        Said(await _f.MarkAsync()).Should().Be((StatusCodes.Status409Conflict,
            "7 already counts as landed: merged on 2026-10-13 09:00:00Z."));
        _f.Rows().Should().ContainSingle().Which.Kind.Should().Be(TicketLandingKinds.Merged);
    }

    [Fact]
    public async Task LandedByHand_SecondMark_Is409AndKeepsTheFirstNote()
    {
        Landed(await _f.MarkAsync("first"));
        _f.Clock.Now = T.AddHours(2);

        Said(await _f.MarkAsync("second", user: Person(("sub", "u-9"), ("name", "Grace")))).Should().Be((StatusCodes.Status409Conflict,
            "7 is already marked done by Ada Lovelace at 2026-10-13 10:00:00Z: first."));
        _f.Rows().Should().ContainSingle().Which.Note.Should().Be("first");
    }

    [Fact]
    public async Task LandedByHand_TicketWithoutRunsPrRecordsOrFilingRecord_IsRecorded()
    {
        var result = await _f.MarkAsync(ticket: "31");

        Landed(result).TicketKey.Should().Be("github-31", "no run, no pull-request record and no approval record exist for 31");
    }

    [Fact]
    public async Task LandedByHand_PublishesTicketLandedWithKindByHandAndThePerson()
    {
        await _f.MarkAsync();

        _f.Events.Verify(e => e.PublishAsync(It.Is<TicketLandedEvent>(l =>
            l.Kind == TicketLandingKinds.ByHand && l.By == "Ada Lovelace" && l.TicketId == "7" && l.Project == "p"),
            It.IsAny<CancellationToken>()), Times.Once());
        _f.Activity.Verify(a => a.ChangedAsync("p", "github-7", It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task LandedByHand_RequestCancelledAfterTheTrackerRead_StillWakesTheDependent()
    {
        await _f.HeldAsync("8", "7");
        using var request = new CancellationTokenSource();
        var trackers = new Mock<ITicketProviderFactory>();
        trackers.Setup(t => t.Create(It.IsAny<AgentSmith.Contracts.Models.Configuration.TrackerConnection>()).GetTicketAsync(
                It.IsAny<TicketId>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                request.Cancel();
                return Task.FromResult(new Ticket(new TicketId("7"), "t", "d", null, "open", "GitHub"));
            });

        Landed(await _f.MarkAsync(trackers: trackers.Object, ct: request.Token));

        _f.Nudged.Should().ContainSingle().Which.TicketId.Should().Be("8");
    }
}
