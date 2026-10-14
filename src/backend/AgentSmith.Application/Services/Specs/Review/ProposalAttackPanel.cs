using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: one round's attack reviewers over every open draft of a design turn's proposal — reviewers
/// fresh calls per draft, ALL under one semaphore of max_concurrent, each on its own
/// <see cref="DerivationLookFactory.ForProposalAttack"/> look. Answers come back per draft in reviewer order; a
/// reviewer not taken, or one that failed, is null.
/// </summary>
public sealed class ProposalAttackPanel(
    ISpecAttackReviewer reviewer, DerivationLookFactory looks, ILogger<ProposalAttackPanel> logger)
{
    public async Task<IReadOnlyList<SpecAttackResult?>[]> ReviewAsync(
        ProposalTurn turn, IReadOnlyList<SpecAttackInput> inputs, SpecReviewConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(inputs);
        using var throttle = new SemaphoreSlim(config.MaxConcurrent);
        var drafts = inputs.Select(async input => (IReadOnlyList<SpecAttackResult?>)await Task.WhenAll(
            Enumerable.Range(0, config.Reviewers).Select(index => OneAsync(turn, input with { Reviewer = index }, throttle, ct))));
        return await Task.WhenAll(drafts);
    }

    private async Task<SpecAttackResult?> OneAsync(
        ProposalTurn turn, SpecAttackInput input, SemaphoreSlim throttle, CancellationToken ct)
    {
        await throttle.WaitAsync(ct);
        try
        {
            await using var look = looks.ForProposalAttack(turn.Pipeline, input.Draft);
            return await reviewer.ReviewAsync(input, look, turn.Agent, turn.CostTracker, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "A review of the design turn's draft {SpecId} could not be taken", input.Draft.SpecId);
            return null;
        }
        finally
        {
            throttle.Release();
        }
    }
}
