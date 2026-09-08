using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-08-5cd2: the fingerprint is the ticket text the model last cut from, and the
/// revision cause reads it — an edit outranks the sha, a resume outranks the edit, and
/// a set without a fingerprint compares as unchanged.
/// </summary>
public sealed class TicketTextFingerprintTests
{
    private const string Before = "Users are signed out mid-session. Fix all other severities only if it's worth it.";
    private const string After = "Users are signed out mid-session.";

    [Fact]
    public void Fingerprint_WhitespaceAndMarkupDifferences_AreTheSameText()
    {
        var plain = TicketTextFingerprint.Of(Ticket("Users are signed out\n\nmid-session."));
        var spaced = TicketTextFingerprint.Of(Ticket("  Users   are signed out \r\n mid-session.  "));
        var markup = TicketTextFingerprint.Of(Ticket("<p>Users are signed out</p><p>mid-session.</p>"));

        spaced.Should().Be(plain, "a re-save that changes nothing a reader sees is not an edit");
        markup.Should().Be(plain, "a provider's HTML and its text are the same ticket");
    }

    [Fact]
    public void Fingerprint_ADifferentDescription_IsADifferentText()
    {
        TicketTextFingerprint.Of(Ticket(Before)).Should().NotBe(TicketTextFingerprint.Of(Ticket(After)));
        TicketTextFingerprint.Of(Ticket(After, title: "Another title"))
            .Should().NotBe(TicketTextFingerprint.Of(Ticket(After)), "the title is part of what the model read");
    }

    [Fact]
    public void Cause_ATicketWhoseTextChanged_IsATicketEdit()
    {
        var previous = Previous(TicketTextFingerprint.Of(Ticket(Before)));

        SpecRevisionCause.For(previous, Pointer("sha-1"), Ticket(After), new PipelineContext())
            .Should().Be(SpecRevisionCause.TicketEdit);
        SpecRevisionCause.For(previous, Pointer("another-sha"), Ticket(After), new PipelineContext())
            .Should().Be(SpecRevisionCause.TicketEdit, "the edit is newer input than any commit on the branch");
    }

    [Fact]
    public void Cause_ATicketWhoseTextIsUnchanged_ReadsTheShaAsBefore()
    {
        var previous = Previous(TicketTextFingerprint.Of(Ticket(After)));

        SpecRevisionCause.For(previous, Pointer("sha-1"), Ticket(After), new PipelineContext())
            .Should().Be(SpecRevisionCause.Retrigger);
        SpecRevisionCause.For(previous, Pointer("another-sha"), Ticket(After), new PipelineContext())
            .Should().Be(SpecRevisionCause.ReviewerEdit);
    }

    [Fact]
    public void Cause_ASetWithoutAFingerprint_ReadsTheShaAsBefore()
    {
        var previous = Previous(fingerprint: null);

        SpecRevisionCause.For(previous, Pointer("sha-1"), Ticket(After), new PipelineContext())
            .Should().Be(SpecRevisionCause.Retrigger, "a missing fact is not evidence of an edit");
        SpecRevisionCause.For(previous, null, Ticket(After), new PipelineContext())
            .Should().Be(SpecRevisionCause.ReviewerEdit);
    }

    [Fact]
    public void Cause_AResume_OutranksATicketEdit()
    {
        var previous = Previous(TicketTextFingerprint.Of(Ticket(Before)));
        var resuming = new PipelineContext();
        resuming.Set(ContextKeys.ResumeCheckpoint, "checkpoint-1");

        SpecRevisionCause.For(previous, Pointer("sha-1"), Ticket(After), resuming)
            .Should().Be(SpecRevisionCause.Resume, "a resume continues what the run was doing");
    }

    private static Ticket Ticket(string description, string title = "Token refresh drops the session") =>
        new(new TicketId("1"), title, description, null, "open", "azdo", []);

    private static SpecSetReadResult Previous(string? fingerprint) => new(
        new SpecSet(
            "azdo-1", [], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
            SpecSource.BranchArtifact, TicketFingerprint: fingerprint),
        "sha-1");

    private static SpecSetPointer Pointer(string sha) => new("azdo-1", "primary", sha, 1);
}
