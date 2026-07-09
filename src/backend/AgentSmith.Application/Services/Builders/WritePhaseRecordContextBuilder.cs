using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>p0315d: builds the WritePhaseRecord context from the checked-out repository.</summary>
public sealed class WritePhaseRecordContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repository = pipeline.Get<Repository>(ContextKeys.Repository);
        return new WritePhaseRecordContext(repository, pipeline);
    }
}
