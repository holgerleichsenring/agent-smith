namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0466: a phase plus the spec it executed. The record is served only on the single-
/// phase read — it is the largest thing a phase carries, and a list of phases that
/// shipped every spec body would be a document dump, not an index.
/// <para>
/// <see cref="Record"/> is null when the run wrote none (an ordinary ticket carries no
/// phase spec) — an explicit absence, never an empty string standing in for one.
/// </para>
/// </summary>
public sealed record RunPhaseDetailView(RunPhaseView Phase, string? Record);
