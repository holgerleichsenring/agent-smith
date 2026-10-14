namespace AgentSmith.Contracts.Dialogue;

/// <summary>One change to a ticket's series — <see cref="SeriesEditKinds"/>.</summary>
/// <param name="Kind">recut, drop or append.</param>
/// <param name="SpecId">The spec a recut or drop names; unused by an append.</param>
/// <param name="Yaml">The full spec document of a recut or an append; unused by a drop.</param>
public sealed record SeriesEdit(string Kind, string? SpecId = null, string? Yaml = null);
