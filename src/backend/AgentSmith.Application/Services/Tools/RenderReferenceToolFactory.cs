using AgentSmith.Application.Services.Browser;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-01-283de: builds render_reference for a design turn — one that carries its project, its
/// conversation (the browser sandbox is held under it) and its sandbox map (where its uploaded
/// websites are addressed). A context without them gets no host.
/// <para>
/// 2026-10-01-283df: and for a RUN of a project whose sandbox block enables the browser — config
/// decides, because admission reserved the browser pod before the run started. The run's host
/// addresses the websites the run carries.
/// </para>
/// </summary>
public sealed class RenderReferenceToolFactory(RenderReferenceServices services)
{
    /// <summary>The host for a design turn (<paramref name="isDesignTurn"/>) or a run's coding master, or null.</summary>
    public RenderReferenceToolHost? Create(PipelineContext pipeline, bool isDesignTurn)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var sandboxes = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map) && map is not null
            ? map : new Dictionary<string, ISandbox>();
        if (isDesignTurn)
            return pipeline.TryGet<ResolvedProject>(ContextKeys.SpecDialogProject, out var turnProject) && turnProject is not null
                && pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var conversation) && !string.IsNullOrEmpty(conversation)
                ? new RenderReferenceToolHost(services, new RenderReferenceScope(turnProject, conversation, sandboxes, []))
                : null;
        if (!pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project) || project is null
            || project.Sandbox?.Browser?.Enabled != true)
            return null;
        var carried = pipeline.TryGet<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, out var sets) && sets is not null
            ? sets : [];
        return new RenderReferenceToolHost(services, new RenderReferenceScope(project, null, sandboxes, carried));
    }
}
