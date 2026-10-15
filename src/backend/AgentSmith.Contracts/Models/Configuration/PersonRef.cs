namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-15-4a4da: one person named in a project binding, against the claim the name was
/// written for — the same reasoning as <see cref="PersonGrant"/>: a ref resolves only while
/// <see cref="Claim"/> is the configured name claim, so a value read under another claim
/// never names somebody else.
/// </summary>
public sealed class PersonRef
{
    /// <summary>The claim name this ref was written against — the name claim at the time.</summary>
    public string Claim { get; set; } = string.Empty;

    /// <summary>The claim's value, compared ORDINALLY: it is an identifier, not a word.</summary>
    public string Value { get; set; } = string.Empty;
}
