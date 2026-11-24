using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Server.Services.Pocs.Revisions;

/// <summary>2026-11-23-933dc: GET /api/pocs/{pocId}/revisions/{n} — the revision, its verdict (masked snippets only), the
/// agent's explanation (untrusted text), and whether the caller may overrule it, or why not.</summary>
internal sealed record PocRevisionAnswer(
    long PocId, string Name, int Revision, int LatestRevision, string State, string? RunId, string? CommitSha,
    PocScanVerdict? Verdict, string? Explanation, bool MayOverrule, string? WhyNot)
{
    /// <summary>2026-11-23-933dd: the stage a blocked revision is blocked at ('code' | 'image').</summary>
    public string? BlockedStage { get; init; }

    /// <summary>2026-11-23-933dd: the image scan's verdict — its blocks, or the notes of a pushed image.</summary>
    public PocScanVerdict? ImageVerdict { get; init; }

    public string? Digest { get; init; }

    /// <summary>2026-11-23-933dd: the host pipeline run that built (or is building) the revision.</summary>
    public string? PipelineUrl { get; init; }
}
