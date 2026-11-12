using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-12-cd4cj: review_proposal — the design master asks for the same one advisory pass the post-turn review
/// runs (<see cref="ProposalReviewPass"/>) over its current proposal (<see cref="ReviewedProposalSource"/>) and reads
/// what it found as data. It never changes the proposal; the master decides what to do with the findings.
/// </summary>
public sealed class ReviewProposalToolHost(
    ProposalReviewPass pass, ISpecReviewSettingsReader settings, PipelineContext pipeline, AgentConfig agent) : IToolHost
{
    public const string Name = "review_proposal";
    public const string NothingToReview = "nothing to review: the proposal holds no spec, epic or ticket list";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(ReviewAsync, name: Name)];
    }

    [Description(
        "Runs fresh reviewers once over your current proposal — each spec against the code, a ticket list as a plan — "
        + "and returns what they found: findings per item, each spec's verdict with the cuts it suggests, and what "
        + "could not be reviewed. Advice only: the proposal is not changed.")]
    private async Task<ReviewProposalAnswer> ReviewAsync(CancellationToken ct)
    {
        if (ReviewedProposalSource.Current(pipeline) is not { } proposal || !ProposalReviewPass.Reviewable(proposal))
            return ReviewProposalAnswer.Nothing(NothingToReview);
        var read = new ProposalReviewSettingsReader(settings, NullLogger.Instance).Read();
        if (read.Settings is not { } current) return ReviewProposalAnswer.Nothing($"not reviewed: {read.Problem}");
        var reviewed = await pass.ReviewAsync(
            new ProposalTurn(pipeline, agent, PipelineCostTracker.GetOrCreate(pipeline)), current, proposal, ct);
        return new ReviewProposalAnswer(null, reviewed.Findings, reviewed.ReviewRounds, reviewed.NotReviewed ?? []);
    }
}
