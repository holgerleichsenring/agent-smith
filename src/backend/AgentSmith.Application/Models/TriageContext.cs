using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for triaging a ticket to determine which specialist roles should participate.
/// </summary>
public sealed record TriageContext(
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
