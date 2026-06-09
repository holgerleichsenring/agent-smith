using System.Diagnostics;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0205: visible first step of every preset. Reads the <see cref="CatalogResolution"/>
/// binding this run resolved to (set by ExecutePipelineUseCase after the loader
/// ran), gathers the catalog counts from the loaded skills + vocabulary, and
/// emits a per-run <see cref="CatalogLoadedEvent"/> so the run-detail page can
/// show what THIS run bound to. The catalog itself is already resolved — this
/// step records the binding, it does not re-pull.
/// </summary>
public sealed class LoadCatalogHandler(
    ISkillLoader skillLoader,
    IEventPublisher eventPublisher,
    ILogger<LoadCatalogHandler> logger) : ICommandHandler<LoadCatalogContext>
{
    /// <summary>Catalog-wide skills subtree, resolved against the catalog root by the loader.</summary>
    private const string CatalogSkillsRootSubPath = "skills";
    private const string MasterRole = "master";

    public async Task<CommandResult> ExecuteAsync(LoadCatalogContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        // ExecutePipelineUseCase sets this immediately after EnsureResolvedAsync.
        // Guard like the RunId guards below so context-seeding harnesses that
        // bypass the resolver (PipelineRunner) don't crash on the first step.
        if (!pipeline.TryGet<CatalogResolution>(ContextKeys.CatalogResolution, out var resolution)
            || resolution is null)
        {
            logger.LogDebug("No CatalogResolution in context — catalog binding unavailable for this run");
            return CommandResult.Ok("catalog binding unavailable");
        }

        var sw = Stopwatch.StartNew();
        var roles = skillLoader.LoadRoleDefinitions(CatalogSkillsRootSubPath);
        var masterNames = SortedNames(roles.Where(IsMaster));
        var skillNames = SortedNames(roles.Where(r => !IsMaster(r)));
        var conceptNames = ConceptNames(pipeline);
        sw.Stop();

        await PublishCatalogLoadedAsync(
            pipeline, resolution, skillNames, masterNames, conceptNames, sw.ElapsedMilliseconds, cancellationToken);

        logger.LogInformation(
            "Catalog {Version} ({Source}): {Concepts} concepts, {Skills} skills, {Masters} masters, fromCache={FromCache}",
            resolution.Version, resolution.Source, conceptNames.Count, skillNames.Count, masterNames.Count, resolution.FromCache);
        return CommandResult.Ok(
            $"catalog {resolution.Version}: {conceptNames.Count} concepts, {skillNames.Count} skills, {masterNames.Count} masters");
    }

    private static bool IsMaster(RoleSkillDefinition role) =>
        string.Equals(role.Role, MasterRole, StringComparison.Ordinal);

    private static IReadOnlyList<string> SortedNames(IEnumerable<RoleSkillDefinition> roles) =>
        roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<string> ConceptNames(PipelineContext pipeline) =>
        pipeline.TryGet<ConceptVocabulary>(ContextKeys.ConceptVocabulary, out var vocab)
            ? vocab!.Concepts.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray()
            : [];

    private Task PublishCatalogLoadedAsync(
        PipelineContext pipeline, CatalogResolution resolution,
        IReadOnlyList<string> skillNames, IReadOnlyList<string> masterNames,
        IReadOnlyList<string> conceptNames, long durationMs, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return Task.CompletedTask;
        return eventPublisher.PublishAsync(
            new CatalogLoadedEvent(
                runId, resolution.Version, resolution.Source.ToString(), resolution.SourceUrl,
                conceptNames.Count, skillNames.Count, masterNames.Count,
                resolution.FromCache, durationMs, DateTimeOffset.UtcNow,
                skillNames, masterNames, conceptNames),
            ct);
    }
}
