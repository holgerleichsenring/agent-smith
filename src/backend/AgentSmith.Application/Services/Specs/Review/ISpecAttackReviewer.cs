using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: one fresh reviewer attacks one spec before it is built and answers BUILD, BUILD WITH
/// CUTS or REFUSE with evidence it read itself. A port, because it is a model call the framework makes on
/// its own behalf. The look is the CALLER's, per call — a run and a design turn each open the look they
/// can — and a null look is a review not taken: there is nothing to attack with. Null is returned too
/// when the call fails or its answer cannot be read; a review never fails its caller.
/// </summary>
public interface ISpecAttackReviewer
{
    Task<SpecAttackResult?> ReviewAsync(
        SpecAttackInput input, DerivationLook? look, AgentConfig agent, PipelineCostTracker costTracker,
        CancellationToken cancellationToken);
}
