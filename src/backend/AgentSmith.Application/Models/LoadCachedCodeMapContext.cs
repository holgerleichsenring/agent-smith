using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>p0315b: context for the LoadCachedCodeMap spec-dialog grounding step.</summary>
public sealed record LoadCachedCodeMapContext(PipelineContext Pipeline) : ICommandContext;
