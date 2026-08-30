using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-08-30-0ea8: the published verification standard this binary ships, read from a
/// checked-in copy whose digest the build verifies. Nothing downloads it at runtime: the
/// upstream is a third party that can replace or delete a release asset.
/// </summary>
public interface IVerificationCatalogue
{
    /// <summary>The upstream release the entries were ingested from.</summary>
    string Version { get; }

    /// <summary>Every entry of that release, verbatim, in the order it publishes them.</summary>
    IReadOnlyList<VerificationRequirement> Requirements { get; }
}
