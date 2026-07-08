using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>p0315b: builder for the LoadCachedCodeMap spec-dialog grounding step.</summary>
public sealed class LoadCachedCodeMapContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new LoadCachedCodeMapContext(pipeline);
}

/// <summary>p0315b: builder for the CollectSpecDialogReply step.</summary>
public sealed class CollectSpecDialogReplyContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new CollectSpecDialogReplyContext(pipeline);
}
