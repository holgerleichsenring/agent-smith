using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// p0202: builds the EnsurePrerequisites context. Like SetupRegistryAuth, the
/// handler reads everything it needs (per-context ProjectMaps + sandboxes)
/// from the pipeline, so the builder only forwards the pipeline.
/// </summary>
public sealed class EnsurePrerequisitesContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new EnsurePrerequisitesContext(pipeline);
}
