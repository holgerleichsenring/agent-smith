namespace AgentSmith.Contracts.Specs;

/// <summary>2026-10-13-8762j: a contract narrowed to a set's members, and what the narrowing removed.</summary>
/// <param name="Contract">The narrowed contract; null when nothing is left of it.</param>
/// <param name="Removed">One line per removed entry or member reference, for the log.</param>
public sealed record SeriesContractWithin(SeriesContract? Contract, IReadOnlyList<string> Removed);
