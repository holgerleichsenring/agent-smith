namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-18-78efb: one load of the values stored in the admin area — the ones that opened, keyed by the secret's
/// name in lower case, and the ones that exist but did not open, keyed the same way with a reason that names no value.
/// A secret in neither has no stored value at all.
/// </summary>
public sealed record StoredSecretLoad(IReadOnlyDictionary<string, string> Values, IReadOnlyDictionary<string, string> Unreadable)
{
    public static StoredSecretLoad Empty { get; } = new(new Dictionary<string, string>(), new Dictionary<string, string>());

    /// <summary>Counts only: a load holds secret values, and a record's generated text would print them.</summary>
    public override string ToString() => $"{Values.Count} stored value(s), {Unreadable.Count} unreadable";
}
