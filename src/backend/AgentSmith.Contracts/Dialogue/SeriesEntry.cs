using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Dialogue;

/// <summary>One spec of a ticket's series.</summary>
/// <param name="State">The spec's run state as the latest cut's attempt recorded it — done, failed, handed_back,
/// in_progress, not_started or carried; not_started for a spec no attempt cut yet.</param>
/// <param name="Ran">Whether the spec already ran on the ticket branch — what an edit can no longer change.</param>
public sealed record SeriesEntry(SpecDraft Draft, string State, bool Ran);
