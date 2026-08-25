namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0515: how a CONFIGURED name — a project, repo, tracker, connection or agent key — is
/// matched. One rule, in one place, so a catalog, a lookup and an editor can never disagree
/// about whether two spellings name the same entity.
/// <para>
/// The rule is <see cref="StringComparer.OrdinalIgnoreCase"/>. Not the current culture: the
/// container's locale is not the operator's, and a Turkish culture answers that 'I' and 'i'
/// are different letters. Not the invariant culture either: that comparison is linguistic
/// and equates names separated by a soft hyphen. A configuration key is an identifier, and
/// an identifier is compared byte for byte with the case folded away.
/// </para>
/// <para>
/// There is deliberately no second form of the rule. A grouping key derived by uppercasing
/// looks equivalent and is not: invariant uppercasing folds U+017F (LATIN SMALL LETTER LONG S)
/// onto S while ordinal-ignore-case does not, so a detector keyed that way drops a pair the
/// catalogs would have kept apart. Callers that need to group take <see cref="Comparer"/>.
/// </para>
/// </summary>
public static class ConfigNames
{
    /// <summary>The comparer every catalog keyed by a configured name is built with.</summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>The same rule for a direct <see cref="string.Equals(string?, string?, StringComparison)"/>.</summary>
    public static StringComparison Comparison => StringComparison.OrdinalIgnoreCase;

    /// <summary>Whether two configured names denote the same entity.</summary>
    public static bool AreSame(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A name-keyed map re-keyed under <see cref="Comparer"/>, built entry by entry — the
    /// dictionary copy constructor THROWS on a pair of keys differing only in case, and a
    /// throw while reading configuration is a dead process rather than a reported fault.
    /// Such a pair collapses here (the last entry wins), so this is for a LOOKUP whose
    /// collisions something else reports; a catalog that must not guess drops both halves.
    /// </summary>
    public static Dictionary<string, TValue> KeyedByName<TValue>(
        IReadOnlyDictionary<string, TValue> source)
    {
        var result = new Dictionary<string, TValue>(source.Count, Comparer);
        foreach (var (name, value) in source) result[name] = value;
        return result;
    }
}
