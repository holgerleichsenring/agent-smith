namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0328: the ratified expectation per run — first-class run data. Projected
/// from ExpectationRatifiedEvent (unique RunId; a replayed event converges on
/// the same row). Outcome vocabulary: verbatim / edited / rejected /
/// unratified; DraftJson/RatifiedJson are serialized ExpectationDraft payloads
/// and EditDistance the Levenshtein between their canonical renderings — the
/// raw material for the p0329 first-PR-acceptance metric.
/// </summary>
public sealed class RunExpectation : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string DraftJson { get; set; } = string.Empty;
    public string RatifiedJson { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string RatifiedBy { get; set; } = string.Empty;
    public DateTimeOffset RatifiedAt { get; set; }
    public int EditDistance { get; set; }
}
