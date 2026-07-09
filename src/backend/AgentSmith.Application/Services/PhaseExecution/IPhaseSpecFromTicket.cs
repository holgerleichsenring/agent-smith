namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0315d: extracts the validated phase spec out of a phase-labelled ticket
/// body — the exact inverse of the p0315c PhaseTicketRenderer contract (the
/// body ends with exactly ONE fenced ```yaml block holding the spec
/// verbatim). Only phase tickets are ever passed here; routing keys on the
/// label, so non-phase tickets never touch this path.
/// </summary>
public interface IPhaseSpecFromTicket
{
    PhaseSpecExtraction Extract(string ticketBody);
}
