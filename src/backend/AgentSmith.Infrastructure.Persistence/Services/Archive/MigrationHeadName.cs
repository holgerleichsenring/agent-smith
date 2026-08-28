namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: the schema state of a store, expressed as something two providers can
/// compare.
/// <para>
/// The two providers keep separate migration assemblies with disjoint histories, and even
/// a migration they share carries a different timestamp prefix on each side
/// (<c>20260826083039_AddObservedCallers</c> against <c>20260826083043_AddObservedCallers</c>).
/// Comparing recorded ids would refuse every SQLite-to-SQL-Server import, which is the
/// entire journey the archive exists for. So the head is named by what comes AFTER the
/// prefix.
/// </para>
/// </summary>
public sealed class MigrationHeadName
{
    private const char PrefixSeparator = '_';

    /// <summary>
    /// The name of the newest migration in <paramref name="migrationIds"/>, prefix removed.
    /// Empty when the set is empty — an unmigrated store has no schema state to name.
    /// </summary>
    public string Of(IEnumerable<string> migrationIds)
    {
        ArgumentNullException.ThrowIfNull(migrationIds);
        var head = migrationIds.OrderBy(id => id, StringComparer.Ordinal).LastOrDefault();
        return head is null ? string.Empty : WithoutPrefix(head);
    }

    private static string WithoutPrefix(string migrationId)
    {
        var separator = migrationId.IndexOf(PrefixSeparator, StringComparison.Ordinal);
        return separator < 0 ? migrationId : migrationId[(separator + 1)..];
    }
}
