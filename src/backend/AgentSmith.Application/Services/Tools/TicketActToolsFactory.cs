using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// The tools a design master acts on its bound ticket with, each built from the port its turn was seeded with:
/// start_next_attempt, derive_specs (2026-10-26-bdd0g), read_series, verify_series and edit_series. The MASTER only — a child acts on no one's
/// word, so no child surface carries them. A turn seeded with no port gets none of them.
/// </summary>
public sealed class TicketActToolsFactory(SeriesVerification verification)
{
    public IReadOnlyList<AITool> Tools(AgenticMasterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pipeline = context.Pipeline;
        var tools = new List<AITool>();
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
