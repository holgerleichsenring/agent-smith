namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-c81ee: why the lifetime pass started a stop or a delete — the seed ContextKeys.PocLifetimeCase carries it
/// into c81ed's poc-stop or poc-delete, whose act stays stop or delete: an expired PoC whose owner saw no reminder is
/// stopped (<see cref="ExpiryStop"/>) and deleted unseen_delete_after_days after that stop (<see cref="UnseenDelete"/>),
/// one whose owner saw a reminder is deleted (<see cref="SeenDelete"/>), and a blocked PoC that never deployed is removed
/// once its upload is purged (<see cref="Removal"/>).
/// </summary>
public static class PocLifetimeCases
{
    public const string ExpiryStop = "expiry-stop";
    public const string SeenDelete = "seen-delete";
    public const string UnseenDelete = "unseen-delete";
    public const string Removal = "removal";

    public static IReadOnlyList<string> All { get; } = [ExpiryStop, SeenDelete, UnseenDelete, Removal];

    public static bool IsCase(string? text) => text is not null && All.Contains(text, StringComparer.Ordinal);

    /// <summary>The act (PocActKinds) a case is carried out by.</summary>
    public static string ActOf(string lifetimeCase) => lifetimeCase == ExpiryStop ? PocActKinds.Stop : PocActKinds.Delete;
}
