using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>p0390: builds the DeriveSpecification context from the pipeline state.</summary>
public sealed class DeriveSpecificationContextBuilder : IContextBuilder
{
    public ICommandContext Build(
        PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pipeline);
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is not null ? r : project.Repos;
        return new DeriveSpecificationContext(ticket, repos, project.Agent, pipeline);
    }
}
