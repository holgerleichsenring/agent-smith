using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Admin;

/// <summary>2026-11-17-f3a8c: an admin turn whose draft holds changes proposes them; one with none answers.</summary>
public sealed class ConfigDraftOutcome : IAdminTurnOutcome
{
    public OutcomeProposal Of(PipelineContext pipeline) =>
        pipeline.TryGet<AdminConfigDraft>(ContextKeys.AdminConfigDraft, out var draft) && draft is { Items.Count: > 0 }
            ? new ConfigChangeOutcome([.. draft.Items])
            : new AnswerOutcome();
}
