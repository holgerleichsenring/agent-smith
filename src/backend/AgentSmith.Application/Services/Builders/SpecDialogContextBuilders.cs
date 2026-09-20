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

/// <summary>2026-09-19-4c1f: builder for the spec-dialog grounding step. It resolves
/// nothing — the step reads the turn's scope off the pipeline the turn seeded.</summary>
public sealed class GroundSpecDialogContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new GroundSpecDialogContext(pipeline);
}

/// <summary>p0315b: builder for the CollectSpecDialogReply step.</summary>
public sealed class CollectSpecDialogReplyContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new CollectSpecDialogReplyContext(pipeline);
}
