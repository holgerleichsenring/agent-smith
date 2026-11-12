using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: the design turn a proposal review runs inside — its pipeline, the agent and the tracker
/// every reviewer call is charged to. 2026-11-12-cd4ci: nothing re-prompts, so it carries no request or thread.</summary>
public sealed record ProposalTurn(PipelineContext Pipeline, AgentConfig Agent, PipelineCostTracker CostTracker);
