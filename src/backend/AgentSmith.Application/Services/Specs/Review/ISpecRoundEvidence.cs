using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762g: a review round's evidence check (<see cref="SpecRoundEvidence"/>), the seam the
/// review loop runs it through — once on the text the reviewers see, again only when a round changed it.</summary>
public interface ISpecRoundEvidence
{
    Task<SpecRoundEvidenceResult> CheckAsync(PipelineContext pipeline, SpecDraft draft, SpecSet set, CancellationToken ct);
}
