namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-c81ee: what a PoC's lifetime is read from and the two writes it has — an extension (by the poc-extend run)
/// and the owner's seen reminder (by the reminders/seen route). Every expiry is computed in memory over what this reads:
/// no query compares a DateTimeOffset.
/// </summary>
public interface IPocLifetimeStore
{
    /// <summary>The live PoCs (PocStates.IsLive), oldest first.</summary>
    Task<IReadOnlyList<PocLifetimeSubject>> LiveAsync(CancellationToken ct);

    Task<PocLifetimeSubject?> ReadAsync(long pocId, CancellationToken ct);

    /// <summary>One extension: Extensions + 1 and the new expiry.</summary>
    Task ExtendAsync(long pocId, long expiresAtTicks, CancellationToken ct);

    Task RecordReminderSeenAsync(long pocId, long seenAtTicks, CancellationToken ct);
}
