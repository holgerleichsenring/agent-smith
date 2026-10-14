using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: one round's reviewers, in parallel — spec_review.reviewers fresh calls under a semaphore
/// of spec_review.max_concurrent, each on a look of its OWN over the spec's terms, so one reviewer's reads
/// are never another's evidence. Answers come back in reviewer order; a reviewer not taken is null, and its
/// siblings are kept.
/// </summary>
public sealed class SpecReviewPanel(ISpecAttackReviewer reviewer, DerivationLookFactory looks)
{
    /// <param name="input">The round's input; each reviewer gets it with its own index.</param>
    public async Task<IReadOnlyList<SpecAttackResult?>> ReviewAsync(
        PipelineContext pipeline, SpecAttackInput input, SpecReviewConfig config, AgentConfig agent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(config);
        using var throttle = new SemaphoreSlim(config.MaxConcurrent);
        var answers = Enumerable.Range(0, config.Reviewers).Select(index =>
            OneAsync(pipeline, input with { Reviewer = index }, agent, throttle, cancellationToken));
        return await Task.WhenAll(answers);
    }

    private async Task<SpecAttackResult?> OneAsync(
        PipelineContext pipeline, SpecAttackInput input, AgentConfig agent, SemaphoreSlim throttle, CancellationToken ct)
    {
        await throttle.WaitAsync(ct);
        try
        {
            await using var look = looks.OnTerms(pipeline, DerivationLookTerms.AttackReviewFor(input.Draft));
            return await reviewer.ReviewAsync(input, look, agent, PipelineCostTracker.GetOrCreate(pipeline), ct);
        }
        finally
        {
            throttle.Release();
        }
    }
}
