using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Holds;

/// <summary>
/// 2026-11-23-31dci: when a wait on nobody is the admin's — it waits on something outside the run (the model place, sandbox
/// capacity, the restart after a stopped server) and has done so longer than <c>holds.escalate_after_minutes</c>. Read with
/// the hold (<see cref="TicketHoldReader"/>), never stored: nothing runs in the background and the startup changes nothing.
/// </summary>
public static class TicketHoldEscalations
{
    /// <summary>The nobody cases that wait on something outside the run. A working attempt is bounded by its own wall time;
    /// a merge or a requirement still worked carries its own hold.</summary>
    public static bool Escalating(string @case) =>
        @case is TicketHoldCases.ModelWait or TicketHoldCases.Queued or TicketHoldCases.Resuming or TicketHoldCases.Restarting;

    /// <summary>A nobody hold whose escalating wait has lasted longer than the configured minutes is the admin's, its case
    /// still the wait's and its escalation naming the value and the key; any other hold, a wait with no since, a read with
    /// no clock and every hold while the minutes are 0 stay as they are.</summary>
    public static TicketHold? Of(TicketHold? hold, TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (hold is not { Addressee: TicketHoldAddressees.Nobody, Since: { } since } || !Escalating(hold.Case)) return hold;
        if (facts.EscalateAfterMinutes <= 0 || facts.Now is not { } now) return hold;
        return now - since > TimeSpan.FromMinutes(facts.EscalateAfterMinutes)
            ? hold with
            {
                Addressee = TicketHoldAddressees.Admin,
                Escalation = new TicketHoldEscalation(facts.EscalateAfterMinutes, HoldsConfig.EscalateAfterMinutesKey),
            }
            : hold;
    }
}
