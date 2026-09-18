using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: reviews a CUT before it is built. A port, because it is a model call the
/// framework makes on its own behalf — a test drives the pipeline without also having to
/// script the framework's private conversations.
/// <para>
/// 2026-09-15-ffa7: <c>look</c> is the reviewer's OWN look, handed per call because the
/// reviewer keeps nothing between calls; null reviews the text against the text.
/// </para>
/// <para>
/// 2026-09-15-a5a5: it takes the drafts and the key it reads, not a SpecSet — a draft never
/// derived from a ticket has no revision, accounting or source to fabricate. A null or blank
/// ticket is a review with no ticket behind it.
/// </para>
/// </summary>
public interface ISpecCutReviewer
{
    Task<SpecCutReview> ReviewAsync(
        IReadOnlyList<PhaseDraft> drafts, string key, string? ticketText, DerivationLook? look,
        AgentConfig agent, PipelineCostTracker costTracker, CancellationToken cancellationToken);
}
