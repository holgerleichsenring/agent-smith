using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762i: one reply of the design partner and the proposal it resolved to.</summary>
public sealed record ProposalReply(AgenticLoopResult Result, OutcomeProposal Proposal);
