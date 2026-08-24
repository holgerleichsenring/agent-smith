using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0504: the domain profiles carried by the resolved skills catalog. Rooted at
/// <see cref="ISkillsCatalogPath"/>, so which profiles exist is a property of the
/// pin a run resolved — which is why <see cref="Origin"/> exists: with four source
/// modes, "the pin" names nothing an operator can check.
/// </summary>
public interface IDomainProfileCatalog
{
    /// <summary>The resolved catalog this reads from, for refusal messages.</summary>
    string Origin { get; }

    /// <summary>The profile for a declared domain, or null when the catalog has none.</summary>
    DomainProfile? Find(string domain);

    /// <summary>Every domain the resolved catalog carries, for refusal messages.</summary>
    IReadOnlyList<string> KnownDomains { get; }
}
