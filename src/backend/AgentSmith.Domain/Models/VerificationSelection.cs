namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-0ea8: the entries the lens selected for one station, bounded in number,
/// with the two things that must travel wherever their text goes.
/// <para>
/// <paramref name="CatalogueVersion"/> is the release the entries came from: 5.0 of the
/// standard renumbered its predecessor, so a requirement id without a version cites
/// nothing. <paramref name="Attribution"/> is the licence line the ingested text carries
/// — a report that quotes a requirement carries it too.
/// </para>
/// </summary>
public sealed record VerificationSelection(
    string CatalogueVersion,
    string Attribution,
    IReadOnlyList<VerificationRequirement> Requirements)
{
    public static VerificationSelection Empty { get; } = new(string.Empty, string.Empty, []);
}
