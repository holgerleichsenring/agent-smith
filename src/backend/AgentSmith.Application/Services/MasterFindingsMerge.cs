using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0429: what the merge delivered, and which of it nobody vouched for.
/// <para>
/// <see cref="Promoted"/> is the set the scan master's SILENCE put into delivery — it
/// never addressed those locations, and until p0429 that silence was read as "not
/// covered" and shipped them, nine false criticals at a time. They are still delivered
/// by default (silence must not decide in the other direction either), but they are
/// named, so the substantiation step knows exactly which findings have no author.
/// </para>
/// </summary>
public sealed record MasterFindingsMerge(
    IReadOnlyList<SkillObservation> Delivered,
    IReadOnlyList<SkillObservation> Promoted,
    int SuppressedAsReviewed);
