using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// The design master's own acts, each built from what its turn was seeded with: on its bound ticket start_next_attempt,
/// derive_specs (2026-10-26-bdd0g), read_series, verify_series and edit_series; and name_conversation (2026-11-04-96a0a)
/// for a conversation that has no subject yet, bound or not; and state_work_picture (2026-11-05-862ea) on every turn
/// with a reply slot, beside outcome_format (2026-11-05-862eb). The MASTER only — a child acts on no one's word, so no
/// child surface carries them. A turn seeded with none of their ports gets none of them.
/// </summary>
public sealed class TicketActToolsFactory(SeriesVerification verification, ISkillReferenceSource references)
{
    public IReadOnlyList<AITool> Tools(AgenticMasterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pipeline = context.Pipeline;
        var tools = new List<AITool>();
        if (pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot) && slot is not null)
        {
            if (pipeline.TryGet<bool>(ContextKeys.SpecDialogNameable, out var nameable) && nameable)
                tools.AddRange(new NameConversationToolHost(slot).GetTools(null, null));
            tools.AddRange(new WorkPictureToolHost(slot).GetTools(null, null)); // 2026-11-05-862ea: every design turn
            tools.AddRange(new OutcomeFormatToolHost(references).GetTools(null, null));
        }
        if (pipeline.TryGet<IAttemptStart>(ContextKeys.SpecDialogAttemptStart, out var start) && start is not null)
            tools.AddRange(new StartNextAttemptToolHost(start).GetTools(null, null));
        if (pipeline.TryGet<ISpecDerivationStart>(ContextKeys.SpecDialogDerivationStart, out var derive) && derive is not null)
            tools.AddRange(new DeriveSpecsToolHost(derive).GetTools(null, null)); // 2026-10-26-bdd0g
        if (pipeline.TryGet<ISeriesRead>(ContextKeys.SpecDialogSeriesRead, out var series) && series is not null)
        {
            tools.AddRange(new ReadSeriesToolHost(series).GetTools(null, null));
            tools.AddRange(new VerifySeriesToolHost(series, verification, pipeline, context.AgentConfig).GetTools(null, null));
            if (pipeline.TryGet<ISeriesEdit>(ContextKeys.SpecDialogSeriesEdit, out var edit) && edit is not null)
                tools.AddRange(new EditSeriesToolHost(edit).GetTools(null, null));
        }
        return tools;
    }
}
