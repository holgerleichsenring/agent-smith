namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-23-933db: a poc-deploy run validated a revision of a PoC. Its projection writes the PoC's 'imported' audit
/// entry, once per run. <paramref name="Actor"/> is the principal who started the import.
/// </summary>
public sealed record PocImportedEvent(string RunId, DateTimeOffset Timestamp, long PocId, int Revision, string Actor)
    : RunEvent(RunId, EventType.PocImported, Timestamp);
