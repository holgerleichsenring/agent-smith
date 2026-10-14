using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762e: what a spec cites, counted the way an attack review's allowance is sized.</summary>
public static class SpecCitations
{
    /// <summary>The distinct paths the spec's facts cite, each repository qualifier its own path.</summary>
    public static int DistinctPaths(SpecDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var references = new EvidenceReferences();
        return draft.Facts.SelectMany(f => references.Read(f.Evidence).References)
            .Select(r => (r.Qualifier, r.Path)).Distinct().Count();
    }
}
