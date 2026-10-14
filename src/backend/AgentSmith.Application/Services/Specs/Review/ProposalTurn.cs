using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: the design turn a proposal review runs inside — its request and thread, which a
/// re-prompt of the design partner continues, the agent and the tracker every call is charged to.</summary>
public sealed record ProposalTurn(
    PipelineContext Pipeline, AgenticLoopRequest Request, string UserPrompt, MasterConversation Conversation,
    AgentConfig Agent, PipelineCostTracker CostTracker);
