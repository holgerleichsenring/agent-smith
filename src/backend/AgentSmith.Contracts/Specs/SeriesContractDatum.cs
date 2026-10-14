namespace AgentSmith.Contracts.Specs;

/// <summary>2026-10-13-8762j: one datum a series persists — where it lives and which member owns it.</summary>
/// <param name="LivesIn">The table, file or store the datum lives in.</param>
/// <param name="Owner">The spec id of the member that declares and writes the datum.</param>
/// <param name="Writers">The members that also write it; null when none does.</param>
public sealed record SeriesContractDatum(
    string Datum, string LivesIn, string Owner, IReadOnlyList<string>? Writers = null);
