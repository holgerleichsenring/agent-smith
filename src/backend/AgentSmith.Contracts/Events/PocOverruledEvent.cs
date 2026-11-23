namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-23-933dc: an overrule run lifted a blocked PoC revision — the stage whose block it lifted ('code' here,
/// 'image' from 933dd), who, why and exactly which finding ids. Its projection writes the PoC's 'overruled' audit
/// entry, once per run.
/// </summary>
public sealed record PocOverruledEvent(
    string RunId, DateTimeOffset Timestamp, long PocId, int Revision, string Stage, string Actor, string Reason,
    IReadOnlyList<string> FindingIds)
    : RunEvent(RunId, EventType.PocOverruled, Timestamp);
