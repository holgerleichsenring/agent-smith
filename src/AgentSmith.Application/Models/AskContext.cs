using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for asking the human a question from a pipeline step (outside the agentic loop).
/// </summary>
public sealed record AskContext(
    DialogQuestion Question,
    PipelineContext Pipeline) : ICommandContext;
