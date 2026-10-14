using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// One spec as the attempts recorded it, before its verdicts are joined: its served ordinal and state,
/// the row of the attempt that executed it, and its record — parsed, or null with the reason.
/// </summary>
/// <param name="Newest">The row of the newest attempt that carries the spec.</param>
/// <param name="Executed">The newest row that left not_started; null while none has.</param>
/// <param name="Record">The parsed spec record; null when there is none or it could not be read.</param>
/// <param name="RecordBody">The stored record body when it was read, else empty.</param>
/// <param name="RecordNote">Why there is no parsed record; null with one.</param>
public sealed record TicketRunRecordSpec(
    string SpecId,
    int Ordinal,
    string State,
    RunSpec Newest,
    RunSpec? Executed,
    SpecDraft? Record,
    string RecordBody,
    string? RecordNote);
