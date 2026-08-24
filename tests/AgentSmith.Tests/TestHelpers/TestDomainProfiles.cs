using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0504: an in-memory domain-profile catalog, so a test can state which profiles
/// the resolved catalog carries without touching a tarball.
/// </summary>
public sealed class TestDomainProfiles(params DomainProfile[] profiles) : IDomainProfileCatalog
{
    private readonly Dictionary<string, DomainProfile> _byName =
        profiles.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public static IDomainProfileCatalog Empty { get; } = new TestDomainProfiles();

    /// <summary>A coordinator-side resolver over the given profiles.</summary>
    public static ContextDomainResolver Resolver(params DomainProfile[] profiles) =>
        new(new TestDomainProfiles(profiles), NullLogger<ContextDomainResolver>.Instance);

    public string Origin => "(test catalog)";

    public IReadOnlyList<string> KnownDomains =>
        [.. _byName.Keys.OrderBy(k => k, StringComparer.Ordinal)];

    public DomainProfile? Find(string domain) =>
        !string.IsNullOrWhiteSpace(domain) && _byName.TryGetValue(domain, out var profile)
            ? profile : null;
}
