using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-08-30-c6ec: context for the step that states which capability the served interface
/// offers that no declared first-party client exercises. The served description, the repos
/// and their checkouts are all on the pipeline; the agent is carried because the reading of
/// the call sites is a model's, and a model needs the project's agent.
/// </summary>
public sealed record AccountSurfaceDifferenceContext(
    PipelineContext Pipeline, AgentConfig Agent) : ICommandContext;
