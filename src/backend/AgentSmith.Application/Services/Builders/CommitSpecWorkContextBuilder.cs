using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>p0437: builds the CommitSpecWork context — the pipeline is all the checkpointer needs.</summary>
public sealed class CommitSpecWorkContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new CommitSpecWorkContext(pipeline);
}
