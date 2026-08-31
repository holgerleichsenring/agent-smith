using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: reads the consumer declarations off the run's repositories and settles
/// each against the identity of the served description the run holds.
/// <para>
/// The declaration names the interface the way its description titles it, because that is
/// the only identity both sides of the comparison carry. Matching is on the trimmed name,
/// case-insensitively — and nothing looser: a name that ALMOST matches is a name the
/// operator meant for something else.
/// </para>
/// </summary>
public static class ConsumerDeclarations
{
    public static ConsumerResolution Resolve(
        IReadOnlyList<RepoConnection> repos, string servedInterface)
    {
        ArgumentNullException.ThrowIfNull(repos);
        var declared = repos.Where(r => !string.IsNullOrWhiteSpace(r.Consumes)).ToList();
        if (declared.Count == 0) return new ConsumerResolution([], null, AnyDeclared: false);

        var unresolvable = declared.FirstOrDefault(r => !Matches(r.Consumes!, servedInterface));
        return unresolvable is not null
            ? new ConsumerResolution([], unresolvable.Consumes, AnyDeclared: true)
            : new ConsumerResolution([.. declared.Select(r => r.Name)], null, AnyDeclared: true);
    }

    private static bool Matches(string declared, string servedInterface) =>
        !string.IsNullOrWhiteSpace(servedInterface)
        && string.Equals(declared.Trim(), servedInterface.Trim(), StringComparison.OrdinalIgnoreCase);
}
