namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-08-26-7a51: one person's roles at this installation, stored against the CLAIM the
/// grant was written for rather than as a bare value.
/// <para>
/// A bare string would be matched against whatever <c>Identity.Name</c> happens to be
/// today. Written while callers were named by <c>preferred_username</c> and later read
/// under <c>email</c>, <c>alice@example.com</c> can name a different person entirely —
/// the cross-claim collision <c>AGENTSMITH_ADMIN_GRANT</c>'s mandatory prefix already
/// refuses. A grant resolves only while <see cref="Claim"/> is the configured name claim,
/// and one that no longer matches is reported instead of silently granting nothing.
/// </para>
/// </summary>
public sealed class PersonGrant
{
    /// <summary>The claim name this grant was written against — the name claim at the time.</summary>
    public string Claim { get; set; } = string.Empty;

    /// <summary>The claim's value, compared ORDINALLY: it is an identifier, not a word.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>The role names this person holds here, unioned with whatever the directory says.</summary>
    public List<string> Roles { get; set; } = [];
}
