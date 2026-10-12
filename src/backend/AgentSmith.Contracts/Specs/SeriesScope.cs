using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-12-f5f7d: what decides where a series' specs are written — the run's repositories, the
/// repository a spec naming none belongs to, the members the run read off the branch, and the
/// carrying repository the pointer recorded (kept by a set without specs).
/// </summary>
/// <param name="LegacyRepo">The pointer's carrying repository, else the approval's, else the run's
/// only repository; null when none of them names one.</param>
public sealed record SeriesScope(
    IReadOnlyList<RepoConnection> RunRepos,
    string? LegacyRepo,
    IReadOnlyList<string> MembersRead,
    string? PreviousCarrier)
{
    /// <summary>The repository a spec naming none belongs to: the pointer's, else the approval's,
    /// else the only one the run holds.</summary>
    public static string? LegacyOf(
        SpecSetPointer? pointer, string? approvedCarrier, IReadOnlyList<RepoConnection> runRepos) =>
        Named(pointer?.CarryingRepo) ?? Named(approvedCarrier)
        ?? (runRepos is [var only] ? Named(only.Name) : null);

    private static string? Named(string? name) => string.IsNullOrWhiteSpace(name) ? null : name;
}
