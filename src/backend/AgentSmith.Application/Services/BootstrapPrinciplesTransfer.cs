using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0379: transfers the AUTHORED coding principles (universal core + language
/// delta, composed by <see cref="IPrinciplesTemplateSource"/>) into the
/// component's principles.md before the bootstrap skill runs.
/// Principles are authoritative gold — archaeology feeds context.yaml facts
/// only. An existing file is never overwritten (ratified content survives
/// re-init) unless the launch asked for a refresh (2026-10-04-2bf2), which
/// recomposes it and keeps its Project Specifics section; a pre-p0379 catalog without the core template keeps the legacy
/// skill-writes behavior. 2026-10-03-cf20c: the framework overlays the component's manifests
/// declare are composed after the delta, and named only when the file is actually written.
/// </summary>
public sealed class BootstrapPrinciplesTransfer(
    IPrinciplesTemplateSource templates,
    ISkillsCatalogPath catalogPath,
    BootstrapArtefactWriter artefactWriter,
    FrameworkOverlayDetector overlayDetector,
    ILogger<BootstrapPrinciplesTransfer> logger)
{
    private const int WriteTimeoutSeconds = 30;

    // Unresolved is itself an answer: it says the catalog was never bound for this run.
    private string ResolvedCatalogOrigin()
    {
        try { return catalogPath.Origin; }
        catch (InvalidOperationException) { return "unresolved"; }
    }

    public async Task<PrinciplesTransferResult> ApplyAsync(
        PipelineContext pipeline, ISandbox sandbox, string repoName,
        string contextName, string workdir, ProjectMap projectMap, string principlesPath,
        string? existingPrinciples, CancellationToken cancellationToken)
    {
        var language = pipeline.ComponentLanguage(repoName, contextName, projectMap);
        // 2026-10-04-2bf2: an existing file is preserved unless THIS launch asked for a refresh.
        var exists = !string.IsNullOrWhiteSpace(existingPrinciples);
        var preserve = exists && !pipeline.Flag(ContextKeys.RefreshPrinciples);
        // A preserved file is never re-composed, so its overlays are not even looked for.
        var overlays = !preserve
            ? await overlayDetector.DetectAsync(sandbox, workdir, templates.FrameworkOverlays(), cancellationToken)
            : [];
        var composed = templates.Compose(language, overlays);
        if (composed is null)
            return new PrinciplesTransferResult(
                PrinciplesMode.SkillWrites, CatalogOrigin: ResolvedCatalogOrigin());

        // 2026-09-15-d66f: the artefacts are decided per path, so they are applied on EVERY
        // path through this method. A ratified principles.md used to return here and suppress
        // them — in exactly the established repositories they are meant for.
        var artefacts = await artefactWriter.ApplyAsync(
            sandbox, repoName, contextName, composed.Artefacts,
            pipeline.ArtefactsWrittenIn(repoName), cancellationToken);
        pipeline.RememberArtefactWrites(repoName, artefacts);
        if (artefacts.FirstOrDefault(a => a.Status == ArtefactStatus.Refused) is { } refused)
            return new PrinciplesTransferResult(
                PrinciplesMode.SkillWrites,
                $"BootstrapPrinciplesTransfer: artefact '{refused.Path}' refused — {refused.Reason}",
                Artefacts: artefacts);

        if (preserve)
        {
            logger.LogInformation(
                "{Repo}/{Context}: principles.md exists — preserved as ratified, not overwritten",
                repoName, contextName);
            return new PrinciplesTransferResult(
                PrinciplesMode.PreservedExisting, Artefacts: artefacts);
        }

        // The operator's section is copied verbatim; everything above it is catalog text.
        var specifics = ProjectSpecificsSection.Extract(existingPrinciples);
        var content = specifics is null
            ? composed.Content
            : ProjectSpecificsSection.Append(composed.Content, specifics);
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            TimeoutSeconds: WriteTimeoutSeconds,
            Path: principlesPath, Content: content);
        var result = await sandbox.RunStepAsync(step, progress: null, cancellationToken);
        if (result.ExitCode != 0)
            return new PrinciplesTransferResult(
                PrinciplesMode.SkillWrites,
                $"BootstrapPrinciplesTransfer: writing {principlesPath} failed — "
                + (result.ErrorMessage ?? "unknown error"),
                Artefacts: artefacts);

        logger.LogInformation(
            "{Repo}/{Context}: {Verb} composed principles core+{Slug} (delta applied: {DeltaApplied}, overlays: [{Overlays}], project specifics kept: {Kept}) to {Path}",
            repoName, contextName, exists ? "refreshed" : "transferred", composed.LanguageSlug,
            composed.DeltaApplied, string.Join(", ", composed.Overlays), specifics is not null, principlesPath);
        return new PrinciplesTransferResult(
            exists ? PrinciplesMode.Refreshed : PrinciplesMode.Transferred, Artefacts: artefacts,
            Overlays: composed.Overlays, ProjectSpecificsKept: specifics is not null);
    }
}
