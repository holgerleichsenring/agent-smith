using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: rewrites ONE refused spec fresh from the code, the refusal's findings in hand — what the
/// supervised session did with a REFUSE that named nothing to cut. Null when the rewrite was not made or
/// changed what <see cref="SpecIdentityRule"/> holds; the refusal then stands. Never throws for a failed call.
/// </summary>
public interface ISpecRederiver
{
    Task<SpecDraft?> RederiveAsync(
        PipelineContext pipeline, SpecDraft draft, IReadOnlyList<SpecReviewFinding> findings, string? widerGoal,
        AgentConfig agent, CancellationToken cancellationToken);
}
