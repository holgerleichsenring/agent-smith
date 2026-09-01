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
/// re-init); a pre-p0379 catalog without the core template keeps the legacy
/// skill-writes behavior.
/// </summary>
public sealed class BootstrapPrinciplesTransfer(
    IPrinciplesTemplateSource templates,
    ISkillsCatalogPath catalogPath,
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
        string contextName, ProjectMap projectMap, string principlesPath,
        string? existingPrinciples, CancellationToken cancellationToken)
    {
        var language = ResolveComponentLanguage(pipeline, repoName, contextName, projectMap);
        var composed = templates.Compose(language);
        if (composed is null)
            return new PrinciplesTransferResult(
                PrinciplesMode.SkillWrites, CatalogOrigin: ResolvedCatalogOrigin());

        if (!string.IsNullOrWhiteSpace(existingPrinciples))
        {
            logger.LogInformation(
                "{Repo}/{Context}: principles.md exists — preserved as ratified, not overwritten",
                repoName, contextName);
            return new PrinciplesTransferResult(PrinciplesMode.PreservedExisting);
        }

        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.WriteFile,
            TimeoutSeconds: WriteTimeoutSeconds,
            Path: principlesPath, Content: composed.Content);
        var result = await sandbox.RunStepAsync(step, progress: null, cancellationToken);
        if (result.ExitCode != 0)
            return new PrinciplesTransferResult(
                PrinciplesMode.SkillWrites,
                $"BootstrapPrinciplesTransfer: writing {principlesPath} failed — "
                + (result.ErrorMessage ?? "unknown error"));

        logger.LogInformation(
            "{Repo}/{Context}: transferred composed principles core+{Slug} (delta applied: {DeltaApplied}) to {Path}",
            repoName, contextName, composed.LanguageSlug, composed.DeltaApplied, principlesPath);
        return new PrinciplesTransferResult(PrinciplesMode.Transferred);
    }

    // Per-component language from discovery wins; the repo-level ProjectMap
    // primary language is the fallback for pre-discovery fixtures.
    private static string ResolveComponentLanguage(
        PipelineContext pipeline, string repoName, string contextName, ProjectMap projectMap)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
                ContextKeys.DiscoveredComponents, out var perRepo) && perRepo is not null
            && perRepo.TryGetValue(repoName, out var components) && components is not null)
        {
            var component = components.FirstOrDefault(
                c => string.Equals(c.Name, contextName, StringComparison.OrdinalIgnoreCase));
            if (component is not null && !string.IsNullOrWhiteSpace(component.Language))
                return component.Language;
        }
        return projectMap.PrimaryLanguage ?? string.Empty;
    }
}
