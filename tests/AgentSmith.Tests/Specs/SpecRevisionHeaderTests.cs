using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79b: the revision header a run writes onto the set it publishes — the numbering,
/// the cause and the fingerprint that decides whether a kept input is still new.
/// </summary>
public sealed class SpecRevisionHeaderTests
{
    private const string Key = "azdo-19106";

    [Fact]
    public void Finalize_AppendsTheNextRevisionToTheHistory()
    {
        var previous = Read(Set(approved: false) with
        {
            Revisions =
            [
                new SpecRevision(1, SpecRevisionCause.Initial, ApprovedSets.Noon),
                new SpecRevision(2, SpecRevisionCause.Retrigger, ApprovedSets.Noon),
            ],
        });

        var finalized = SpecRevisionHeader.Finalize(
            Set(approved: false), previous, SpecRevisionCause.Retrigger, Ticket(), modelRan: false);

        finalized.Revisions.Select(r => r.Number).Should().Equal(1, 2, 3);
        finalized.Current.Cause.Should().Be(SpecRevisionCause.Retrigger);
    }

    [Fact]
    public void Finalize_AnApprovedSetThatKeptAnInput_SaysSoInTheCause()
    {
        var finalized = SpecRevisionHeader.Finalize(
            Set(approved: true), Read(Set(approved: true)), SpecRevisionCause.Comment, Ticket(),
            modelRan: false, reported: true);

        finalized.Current.Cause.Should().Be($"{SpecRevisionCause.Comment} — {ApprovedSetKept.Kept}");
    }

    [Fact]
    public void Finalize_TheModelRan_RefreshesTheFingerprint()
    {
        var finalized = SpecRevisionHeader.Finalize(
            Set(approved: false), Read(Set(approved: false), "an-older-fingerprint"),
            SpecRevisionCause.TicketEdit, Ticket(), modelRan: true);

        finalized.TicketFingerprint.Should().Be(TicketTextFingerprint.Of(Ticket()));
    }

    [Fact]
    public void Finalize_NoModelAndNoKeptEdit_CarriesTheFingerprintForward()
    {
        var finalized = SpecRevisionHeader.Finalize(
            Set(approved: true), Read(Set(approved: true), "an-older-fingerprint"),
            SpecRevisionCause.Comment, Ticket(), modelRan: false, reported: true);

        finalized.TicketFingerprint.Should().Be("an-older-fingerprint",
            "a comment does not change the ticket text, and the comment cause clears itself");
    }

    [Fact]
    public void Finalize_AKeptEditThatWasReported_ClearsTheCause()
    {
        var finalized = SpecRevisionHeader.Finalize(
            Set(approved: true), Read(Set(approved: true), "the-text-before-the-edit"),
            SpecRevisionCause.TicketEdit, Ticket(), modelRan: false, reported: true);

        finalized.TicketFingerprint.Should().Be(TicketTextFingerprint.Of(Ticket()));
    }

    [Fact]
    public void Finalize_AKeptEditNobodyWasToldAbout_KeepsTheCause()
    {
        var finalized = SpecRevisionHeader.Finalize(
            Set(approved: true), Read(Set(approved: true), "the-text-before-the-edit"),
            SpecRevisionCause.TicketEdit, Ticket(), modelRan: false, reported: false);

        finalized.TicketFingerprint.Should().Be("the-text-before-the-edit",
            "clearing an input nobody received would lose the edit for good");
    }

    /// <summary>A first revision has no previous fingerprint to carry, so it takes the text it saw.</summary>
    [Fact]
    public void Finalize_NoPreviousSet_TakesTheTicketsCurrentText()
    {
        var finalized = SpecRevisionHeader.Finalize(
            Set(approved: true), null, SpecRevisionCause.Initial, Ticket(), modelRan: false);

        finalized.Revisions.Should().ContainSingle().Which.Number.Should().Be(1);
        finalized.TicketFingerprint.Should().Be(TicketTextFingerprint.Of(Ticket()));
    }

    private static SpecSetReadResult Read(SpecSet set, string? fingerprint = null) =>
        new(set with { TicketFingerprint = fingerprint }, "branch-sha");

    private static SpecSet Set(bool approved) => new(
        Key,
        [ApprovedSets.Phase("p19106a")],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, ApprovedSets.Noon)],
        approved ? SpecSource.Approved : SpecSource.Derived,
        Approval: approved ? ApprovedSets.Approval(ApprovedSets.Noon, "session-77") : null);

    private static Ticket Ticket() =>
        new(new TicketId("19106"), "Migrate the client", "Migrate the client.", null, "open", "azdo", []);
}
