namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0466: a run's spec row plus the spec body it executed. The record is served only on
/// the single-spec read — it is the largest thing a spec carries, and a list that shipped
/// every body would be a document dump, not an index.
/// <para>
/// <see cref="Record"/> is null when the run wrote none (an ordinary ticket carries no
/// spec record) — an explicit absence, never an empty string standing in for one.
/// </para>
/// </summary>
public sealed record RunSpecDetailView(RunSpecView Spec, string? Record);
