using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: builds a look — the bounded read-only tools — over a run's or a turn's repositories, on its
/// holder's terms. 2026-11-19-6b0ba: the spec derivation, its first holder, is gone; the design turn's reviews and
/// the attack reviewer remain.
/// </summary>
public sealed class DerivationLookFactory(
    SandboxTargets targets, ISandboxFileReaderFactory files,
    IPackageEcosystemDetector ecosystems,
    TurnActivityTools turnActivity, // 2026-09-17-042ee: what a look reads is a step
    ILogger<DerivationLook> logger)
{
    /// <summary>
    /// The run's repositories on any holder's own terms, with no template scope unless BORROWED (54beb: the diff
    /// reviewer's, from the run's owner; the look never disposes them). Null when the run has no sandbox.
    /// </summary>
    public DerivationLook? OnTerms(PipelineContext pipeline, DerivationLookTerms terms,
        IReadOnlyDictionary<string, ISourceScopeSandbox>? borrowed = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!targets.TryResolve(pipeline, out var sandboxes, out _)) return null;
        return new DerivationLook(
            Review.ReviewReferences.Beside(pipeline, sandboxes)!, files, ecosystems, logger, borrowed, terms, activity: turnActivity,
            borrowsTemplates: borrowed is not null, names: Review.SpecReviewRepositories.Names(pipeline));
    }

    /// <summary>
    /// 2026-09-17-042ed: the look a design turn's proposal is reviewed with — the turn's own repository sandboxes,
    /// read off <see cref="ContextKeys.Sandboxes"/> directly (a design turn has no discoveries), templates dropped.
    /// 2026-11-10-864f: its uploads join read-only under their address. No audit. Null when it holds neither.
    /// </summary>
    /// <remarks>2026-11-12-cd4cl: on <paramref name="agent"/>'s look allowance.</remarks>
    public DerivationLook? ForProposalReview(PipelineContext pipeline, AgentConfig agent) =>
        ForProposal(pipeline, DerivationLookTerms.ProposalReviewWith(agent.DialogReviewLookAllowance));

    /// <summary>The proposal review's look over repositories the caller opened and disposes — a series
    /// verified at the ticket branch rather than at the turn's default branch, or a ticket plan.</summary>
    public DerivationLook ForReview(IReadOnlyDictionary<string, ISandbox> repositories, AgentConfig agent) =>
        new(repositories, files, ecosystems, logger, templates: null,
            DerivationLookTerms.ProposalReviewWith(agent.DialogReviewLookAllowance), audits: false, activity: turnActivity);

    private DerivationLook? ForProposal(PipelineContext pipeline, DerivationLookTerms terms) =>
        Review.ReviewReferences.Beside(pipeline, ProposalRepositories(pipeline)) is { } readable
            ? new DerivationLook(readable, files, ecosystems, logger, templates: null,
                terms, audits: false, activity: turnActivity, names: Review.SpecReviewRepositories.Names(pipeline))
            : null;

    /// <summary>2026-10-02-3f06c: the turn's repositories as the proposal review reads them, one
    /// filter for the look and the evidence probe alike. Null when the turn holds none.</summary>
    public static IReadOnlyDictionary<string, ISandbox>? ProposalRepositories(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map)
            || map is null) return null;
        var repositories = map.Where(entry => Review.SpecReviewRepositories.IsRepositoryKey(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return repositories.Count == 0 ? null : repositories;
    }
}
