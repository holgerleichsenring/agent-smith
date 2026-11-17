using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Admin;

/// <summary>2026-11-17-f3a8a: an admin turn that can draft nothing ends with its answer.</summary>
public sealed class AnswerOnlyAdminOutcome : IAdminTurnOutcome
{
    public OutcomeProposal Of(PipelineContext pipeline) => new AnswerOutcome();
}
