using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: builds the derivation's tool host over every sandbox of the run —
/// the map <see cref="PhaseAccounting"/> already hands the account's search, read from
/// the pipeline the same way. Null when the run has no sandbox: a derivation over an
/// inline ticket with nothing checked out writes from the ticket alone, as it did.
/// </summary>
public sealed class DerivationLookFactory(
    SandboxTargets targets, ISandboxFileReaderFactory files,
    IPackageEcosystemDetector ecosystems, ProjectTemplateScopes templates,
    ILogger<DerivationLook> logger)
{
    public DerivationLook? Create(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!targets.TryResolve(pipeline, out var sandboxes, out _)) return null;
        logger.LogInformation(
            "The derivation may look into {Count} repositor{Plural}: {Repos}",
            sandboxes.Count, sandboxes.Count == 1 ? "y" : "ies", string.Join(", ", sandboxes.Keys));
        return new DerivationLook(sandboxes, files, ecosystems, logger, templates.For(pipeline));
    }

    /// <summary>
    /// 2026-09-15-ffa7: the cut reviewer's look — the run's repositories on the reviewer's
    /// own terms, and NO template scope. Neither template selection is called: each one
    /// creates a scope per declaration, so a second look through either clones every template
    /// again. A template says what a cut should FOLLOW; the reviewer checks what it ASSUMES.
    /// Null when the run has no sandbox, and the review is the text-against-text one.
    /// </summary>
    public DerivationLook? ForCutReview(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!targets.TryResolve(pipeline, out var sandboxes, out _)) return null;
        return new DerivationLook(
            sandboxes, files, ecosystems, logger, templates: null, DerivationLookTerms.CutReview);
    }
}
