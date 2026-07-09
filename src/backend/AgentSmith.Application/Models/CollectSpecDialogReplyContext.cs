using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>p0315b: context for the CollectSpecDialogReply step.</summary>
public sealed record CollectSpecDialogReplyContext(PipelineContext Pipeline) : ICommandContext;
