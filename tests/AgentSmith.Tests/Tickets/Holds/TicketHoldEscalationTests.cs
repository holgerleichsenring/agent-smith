using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets.Holds;

/// <summary>2026-11-23-31dci: a nobody hold says since when it waits, and a wait on something outside the run that lasts
/// longer than holds.escalate_after_minutes is the admin's — read when the hold is read; 0 never reports one.</summary>
public sealed class TicketHoldEscalationTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-23T00:12:00Z");
    private readonly TicketHoldReader _reader = new();

    private static TicketHoldFacts Facts(string? status, int minutesLater, int escalateAfter = 60) =>
        new("alpha", "42")
        {
            HeadStatus = status, HeadRunId = "r-2", Attempt = 2, EndedAt = T,
            EscalateAfterMinutes = escalateAfter, Now = T.AddMinutes(minutesLater),
        };

    [Fact]
    public void Nobody_EachWait_SaysSinceWhen()
    {
        _reader.Of(Facts(RunStatuses.Running, 5) with { WaitingForModelSince = T })!
            .Should().Match<TicketHold>(h => h.Addressee == TicketHoldAddressees.Nobody && h.Case == TicketHoldCases.ModelWait && h.Since == T);
        _reader.Of(Facts(RunStatuses.Queued, 5) with { EnqueuedAt = T })!
            .Should().Match<TicketHold>(h => h.Case == TicketHoldCases.Queued && h.Since == T);
        _reader.Of(Facts(RunStatuses.WaitingForInput, 5) with { EnqueuedAt = T })!
            .Should().Match<TicketHold>(h => h.Case == TicketHoldCases.Resuming && h.Since == T);
        _reader.Of(Facts("cancelled", 5) with { Interrupted = true, Restarting = true })!
            .Should().Match<TicketHold>(h => h.Case == TicketHoldCases.Restarting && h.Since == T, "since the server stopped it");
        _reader.Of(new TicketHoldFacts("alpha", "43") { WaitsFor = ["#42"], FiledAt = T })!
            .Should().Match<TicketHold>(h => h.Case == TicketHoldCases.Prerequisite && h.Since == T);
        _reader.Of(Facts(RunStatuses.Running, 5))!.Should().Match<TicketHold>(h => h.Case == TicketHoldCases.Working && h.Since == null);
    }

    [Fact]
    public void Escalation_At59MinutesNobody_At61AdminWithValueAndKey()
    {
        _reader.Of(Facts(RunStatuses.Running, 59) with { WaitingForModelSince = T })!.Addressee.Should().Be(TicketHoldAddressees.Nobody);

        var late = _reader.Of(Facts(RunStatuses.Running, 61) with { WaitingForModelSince = T })!;

        late.Should().BeEquivalentTo(new TicketHold(TicketHoldAddressees.Admin, TicketHoldCases.ModelWait, null, T)
        {
            Attempt = 2, Escalation = new TicketHoldEscalation(60, HoldsConfig.EscalateAfterMinutesKey),
        });
        late.Escalation!.Key.Should().Be("holds.escalate_after_minutes");
        _reader.Of(Facts(RunStatuses.Queued, 61) with { EnqueuedAt = T })!.Escalation.Should().NotBeNull();
        _reader.Of(Facts("cancelled", 61) with { Interrupted = true, Restarting = true })!.Addressee.Should().Be(TicketHoldAddressees.Admin);
    }

    [Fact]
    public void Escalation_ZeroMinutes_NeverReports()
    {
        _reader.Of(Facts(RunStatuses.Running, 6000, escalateAfter: 0) with { WaitingForModelSince = T })!
            .Should().Match<TicketHold>(h => h.Addressee == TicketHoldAddressees.Nobody && h.Escalation == null);
        _reader.Of(Facts(RunStatuses.Queued, 6000) with { EnqueuedAt = T, Now = null })!
            .Addressee.Should().Be(TicketHoldAddressees.Nobody, "a read without a clock ages nothing");
    }

    [Fact]
    public void Escalating_TheWaitsOnSomethingOutsideTheRun()
    {
        new[] { TicketHoldCases.ModelWait, TicketHoldCases.Queued, TicketHoldCases.Resuming, TicketHoldCases.Restarting,
                TicketHoldCases.Working, TicketHoldCases.Prerequisite, TicketHoldCases.MergeAfter }
            .Select(TicketHoldEscalations.Escalating).Should().Equal(true, true, true, true, false, false, false);
    }

    [Fact]
    public void Escalated_AWaitWithoutAnOutsideCause_StaysNobodys()
    {
        var facts = Facts(RunStatuses.Running, 600);
        TicketHoldEscalations.Of(new TicketHold(TicketHoldAddressees.Nobody, TicketHoldCases.Working, null, T), facts)!
            .Addressee.Should().Be(TicketHoldAddressees.Nobody, "a working attempt is bounded by its own wall time");
        TicketHoldEscalations.Of(new TicketHold(TicketHoldAddressees.Nobody, TicketHoldCases.Prerequisite, null, T), facts)!
            .Addressee.Should().Be(TicketHoldAddressees.Nobody, "the requirement carries its own hold");
        TicketHoldEscalations.Of(new TicketHold(TicketHoldAddressees.Nobody, TicketHoldCases.Queued, null, null), facts)!
            .Addressee.Should().Be(TicketHoldAddressees.Nobody, "a wait with no since has no age");
        TicketHoldEscalations.Of(null, facts).Should().BeNull();
        var decide = new TicketHold(TicketHoldAddressees.Decide, TicketHoldCases.Failed, null, T);
        TicketHoldEscalations.Of(decide, facts).Should().BeSameAs(decide);
    }
}
