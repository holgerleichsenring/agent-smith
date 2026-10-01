using AgentSmith.Application.Services.Browser;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-01-283de: builds render_reference for a design turn — one that carries its project, its
/// conversation (the browser sandbox is held under it) and its sandbox map (where its uploaded
/// websites are addressed). A context without them gets no host.
/// </summary>
public sealed class RenderReferenceToolFactory(RenderReferenceServices services)
{
    public RenderReferenceToolHost? Create(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<ResolvedProject>(ContextKeys.SpecDialogProject, out var project) || project is null
            || !pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var conversation) || string.IsNullOrEmpty(conversation))
            return null;
        var sandboxes = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map) && map is not null
            ? map : new Dictionary<string, ISandbox>();
        return new RenderReferenceToolHost(services, project, conversation, sandboxes);
    }
}
