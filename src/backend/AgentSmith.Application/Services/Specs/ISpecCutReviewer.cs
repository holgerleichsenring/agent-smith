using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
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
/// </summary>
public interface ISpecCutReviewer
{
    Task<SpecCutReview> ReviewAsync(
        SpecSet set, string ticketText, DerivationLook? look, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken);
}
