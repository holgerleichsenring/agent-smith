namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-8762j: one name a series declares — an event, a table, a context key, an endpoint, a config
/// section, a command, a tool or a type — with the number it takes, if its kind numbers, and the member that
/// declares it.
/// </summary>
/// <param name="Kind">One of <see cref="SeriesContractKinds.All"/>, read case-insensitively.</param>
/// <param name="Number">The value the name takes (an enum member's number); null when its kind has none.</param>
/// <param name="Owner">The spec id of the member that declares the name.</param>
/// <param name="Writers">The members that extend the name the owner declared; null when none does.</param>
public sealed record SeriesContractName(
    string Name, string Kind, int? Number, string Owner, IReadOnlyList<string>? Writers = null);
