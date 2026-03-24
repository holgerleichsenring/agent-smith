using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for triaging a security scan to determine which security specialist roles should participate.
/// </summary>
public sealed record SecurityTriageContext(
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
