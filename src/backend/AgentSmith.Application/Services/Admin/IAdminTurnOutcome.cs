using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Admin;

/// <summary>2026-11-17-f3a8a: what an admin turn proposes when it ends — an answer, or what it drafted.</summary>
public interface IAdminTurnOutcome
{
    OutcomeProposal Of(PipelineContext pipeline);
}
