using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-19-4c1f: grounds one design-conversation turn in the project's own rules. The
/// pinned design-partner master declares the project context and the coding principles as
/// its inputs and renders a heading for each; the preset used to fill neither, so every
/// turn read a heading with nothing under it and drafted specs against rules it could not
/// see. The sandbox-bound loaders cannot serve this preset — two of them are registered as
/// sandbox-requiring, and provisioning REPLACES the turn's lazy read-only scopes with pods
/// holding no source. This step reads the same files remotely instead and provisions
/// nothing. It never fails the turn: what it could not read it reports and continues.
/// </summary>
public sealed class GroundSpecDialogHandler(
    DialogGroundingReader reader,
    ILogger<GroundSpecDialogHandler> logger)
    : ICommandHandler<GroundSpecDialogContext>
{
    public async Task<CommandResult> ExecuteAsync(
        GroundSpecDialogContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pipeline = context.Pipeline;
        var scope = Scope(pipeline);
        if (scope.Count == 0)
            return CommandResult.Ok("No repository in the turn's scope — nothing to ground on");

        var grounded = new List<DialogGrounding>(scope.Count);
        foreach (var repo in scope)
            grounded.Add(await reader.ReadAsync(repo, cancellationToken));

        Publish(pipeline, ContextKeys.RepoContextYamls, ContextKeys.ProjectContext,
            [.. grounded.SelectMany(g => g.Contexts.Documents)]);
        Publish(pipeline, ContextKeys.RepoCodingPrinciples, ContextKeys.DomainRules,
            [.. grounded.SelectMany(g => g.Principles.Documents)]);

        var report = DialogGroundingReport.Compose(grounded);
        logger.LogInformation("Spec-dialog grounding: {Report}", report);
        return CommandResult.Ok(report);
    }

    /// <summary>
    /// The pipeline knows the PROJECT's repositories; the turn seeded its own scope as the
    /// sandbox map. Keeping the repositories that map names recovers the scope without
    /// asking what a key looks like — a declared template is in the map and not in the
    /// list, so it drops out of the intersection by construction rather than by a test on
    /// its name. Without the map (no turn seeded one) the project's own list stands.
    /// </summary>
    private static IReadOnlyList<RepoConnection> Scope(PipelineContext pipeline)
    {
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        return pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map)
               && map is not null
            ? [.. repos.Where(repo => map.ContainsKey(repo.Name))]
            : repos;
    }

    // Both keys carry the typed list and the rendered aggregate the two sandbox-bound
    // loaders publish, through the SAME labelled rendering — a second rendering would show
    // the design partner a differently-shaped block than the coding master is shown.
    private static void Publish(
        PipelineContext pipeline, string listKey, string renderedKey,
        IReadOnlyList<ContextDocument> documents)
    {
        pipeline.Set(listKey, documents);
        if (documents.Count > 0) pipeline.Set(renderedKey, documents.RenderLabelled());
    }
}
