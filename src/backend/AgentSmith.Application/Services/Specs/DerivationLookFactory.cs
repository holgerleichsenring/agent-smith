using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: builds the derivation's tool host over every sandbox of the run —
/// the map <see cref="SpecDeliveryAccounting"/> already hands the account's search, read from
/// the pipeline the same way. Null when the run has no sandbox: a derivation over an
/// inline ticket with nothing checked out writes from the ticket alone, as it did.
/// </summary>
public sealed class DerivationLookFactory(
    SandboxTargets targets, ISandboxFileReaderFactory files,
    IPackageEcosystemDetector ecosystems, ProjectTemplateScopes templates,
    TurnActivityTools turnActivity, // 2026-09-17-042ee: what a look reads is a step
    VerifyStageResolver stageResolver, // 2026-09-20-9c74: the gate's filtering, taken not copied
    ContextVerifyStagesResolver declarations,
    ILogger<DerivationLook> logger)
{
    public DerivationLook? Create(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!targets.TryResolve(pipeline, out var sandboxes, out _)) return null;
        logger.LogInformation(
            "The derivation may look into {Count} repositor{Plural}: {Repos}",
            sandboxes.Count, sandboxes.Count == 1 ? "y" : "ies", string.Join(", ", sandboxes.Keys));
        return new DerivationLook(
            sandboxes, files, ecosystems, logger, templates.For(pipeline), activity: turnActivity);
    }

    /// <summary>
    /// 2026-09-15-ffa7: the cut reviewer's look — the run's repositories on the reviewer's
    /// own terms, and NO template scope. Neither template selection is called: each one
    /// creates a scope per declaration, so a second look through either clones every template
    /// again. A template says what a cut should FOLLOW; the reviewer checks what it ASSUMES.
    /// Null when the run has no sandbox, and the review is the text-against-text one.
    /// </summary>
    public DerivationLook? ForCutReview(PipelineContext pipeline) =>
        OnTerms(pipeline, DerivationLookTerms.CutReview);

    /// <summary>
    /// The look a spec's stated premises are checked with before its work starts: the run's repositories on the premise
    /// check's terms and no template scope (a premise is what the spec assumes, not what it follows). Null when the run
    /// has no sandbox.
    /// </summary>
    public DerivationLook? ForPremiseCheck(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return OnTerms(pipeline, DerivationLookTerms.PremiseCheck, Declared(pipeline));
    }

    /// <summary>
    /// The run's repositories on any holder's own terms, with no template scope; the terms are what tell the holders
    /// apart. Null when the run has no sandbox.
    /// </summary>
    public DerivationLook? OnTerms(
        PipelineContext pipeline, DerivationLookTerms terms, DerivationLookStages? stages = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!targets.TryResolve(pipeline, out var sandboxes, out _)) return null;
        return new DerivationLook(
            sandboxes, files, ecosystems, logger, templates: null, terms, activity: turnActivity,
            stages: stages);
    }

    /// <summary>
    /// What each repository of the run declared, read out of the pipeline here, because the look never sees one.
    /// </summary>
    private DerivationLookStages Declared(PipelineContext pipeline)
    {
        pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map);
        var declared = ((IEnumerable<string>?)map?.Keys ?? []).ToDictionary(
            key => key, key => declarations.For(pipeline, key), StringComparer.Ordinal);
        return new DerivationLookStages(stageResolver, declared);
    }

    /// <summary>
    /// 2026-09-17-042ed: the look a design turn's proposal is reviewed with — the turn's own
    /// repository sandboxes, read off <see cref="ContextKeys.Sandboxes"/> directly, because a
    /// design turn starts no coordinator and so carries no discoveries for
    /// <see cref="SandboxTargets"/> to resolve. The dialog's map holds its templates too; a key
    /// under <see cref="TemplateScopeName.Prefix"/> is the only mark one carries there, so those
    /// are dropped — and 2026-10-01-283dc an uploaded website under <see cref="ReferenceScopeName.Prefix"/> too. No audit: every entry is a read-only source scope that runs no command.
    /// Null when the turn has no repository, and the review is the text-against-text one.
    /// </summary>
    public DerivationLook? ForProposalReview(PipelineContext pipeline) => ForProposal(pipeline, DerivationLookTerms.ProposalReview);

    /// <summary>The proposal review's look over repositories the caller opened and disposes — a series
    /// verified at the ticket branch rather than at the turn's default branch.</summary>
    public DerivationLook ForReview(IReadOnlyDictionary<string, ISandbox> repositories) =>
        new(repositories, files, ecosystems, logger, templates: null,
            DerivationLookTerms.ProposalReview, audits: false, activity: turnActivity);

    /// <summary>2026-10-13-8762i: one attack reviewer's look at one proposal draft, on that draft's attack terms.</summary>
    public DerivationLook? ForProposalAttack(PipelineContext pipeline, Contracts.Models.SpecDraft draft) =>
        ForProposal(pipeline, DerivationLookTerms.AttackReviewFor(draft));

    private DerivationLook? ForProposal(PipelineContext pipeline, DerivationLookTerms terms) =>
        ProposalRepositories(pipeline) is { } repositories
            ? new DerivationLook(repositories, files, ecosystems, logger, templates: null,
                terms, audits: false, activity: turnActivity)
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
