using AgentSmith.Application.Services.Browser;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-01-283de: builds render_reference for a design turn — one that carries its project, its
/// conversation (the browser sandbox is held under it) and its sandbox map (where its uploaded
/// websites are addressed). A context without them gets no host.
/// <para>
/// 2026-10-01-283df: and for a RUN of a project whose sandbox block enables the browser — config
/// decides, because admission reserved the browser pod before the run started. The run's host
/// addresses the websites the run carries. 2026-10-01-283di: compare_reference joins wherever
/// render_reference is, over the same scope and so the same browser sandbox.
/// </para>
/// </summary>
public sealed class RenderReferenceToolFactory(RenderReferenceServices services, CompareReferenceServices compare)
{
    /// <summary>The host for a design turn (<paramref name="isDesignTurn"/>) or a run's coding master, or null.</summary>
    public RenderReferenceToolHost? Create(PipelineContext pipeline, bool isDesignTurn) =>
        Scope(pipeline, isDesignTurn) is { } scope ? new RenderReferenceToolHost(services, scope) : null;

    /// <summary>2026-10-01-283di: render_reference and compare_reference over one scope, or none.</summary>
    public IReadOnlyList<AITool> Tools(PipelineContext pipeline, bool isDesignTurn) =>
        Scope(pipeline, isDesignTurn) is { } scope
            ? [.. new RenderReferenceToolHost(services, scope).GetTools(null, null),
               .. new CompareReferenceToolHost(compare, scope).GetTools(null, null)]
            : [];

    private static RenderReferenceScope? Scope(PipelineContext pipeline, bool isDesignTurn)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var sandboxes = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map) && map is not null
            ? map : new Dictionary<string, ISandbox>();
        if (isDesignTurn)
            return pipeline.TryGet<ResolvedProject>(ContextKeys.SpecDialogProject, out var turnProject) && turnProject is not null
                && pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var conversation) && !string.IsNullOrEmpty(conversation)
                ? new RenderReferenceScope(turnProject, conversation, sandboxes, [], TurnRepos(sandboxes))
                : null;
        if (!pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project) || project is null
            || project.Sandbox?.Browser?.Enabled != true)
            return null;
        var carried = pipeline.TryGet<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, out var sets) && sets is not null
            ? sets : [];
        return new RenderReferenceScope(project, null, sandboxes, carried, RunRepos(pipeline, sandboxes), pipeline);
    }

    // 2026-10-01-283dh: a design turn's map holds its repositories by name, beside template: and
    // reference: addresses; a run's holds sandbox keys, each named to its repository.
    private static Dictionary<string, ISandbox> TurnRepos(IReadOnlyDictionary<string, ISandbox> sandboxes) =>
        sandboxes.Where(s => !s.Key.Contains(':')).ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);

    private static Dictionary<string, ISandbox> RunRepos(PipelineContext pipeline, IReadOnlyDictionary<string, ISandbox> sandboxes)
    {
        var names = pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var r) && r is not null
            ? r : new Dictionary<string, string>();
        var repos = new Dictionary<string, ISandbox>(StringComparer.Ordinal);
        foreach (var (key, sandbox) in sandboxes) repos.TryAdd(names.GetValueOrDefault(key) ?? key, sandbox);
        return repos;
    }
}
