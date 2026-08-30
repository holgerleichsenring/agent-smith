namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-c6ec: the difference between the interface a run holds a served description
/// of and what its declared first-party clients were found to exercise.
/// <para>
/// A run missing either input says so through <paramref name="NotComputedReason"/> instead
/// of reporting an empty difference: nothing read is not the same as nothing found, and
/// the second reads as a clean bill. <paramref name="Account"/> bounds every entry —
/// while it is incomplete the exercised set is a LOWER estimate, so a difference may be
/// an artefact of a file the reading could not decide.
/// </para>
/// </summary>
public sealed record SurfaceDifferenceReport(
    bool Computed,
    string? NotComputedReason,
    IReadOnlyList<SurfaceDifference> Differences,
    ClientExtractionAccount Account,
    string CatalogueVersion)
{
    public static SurfaceDifferenceReport NotComputed(string reason) =>
        new(false, reason, [], ClientExtractionAccount.Empty, string.Empty);

    /// <summary>True while a file the reading could not decide leaves the claim bounded.</summary>
    public bool Degraded => !Account.IsComplete;
}
