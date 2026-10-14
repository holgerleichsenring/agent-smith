using AgentSmith.Application.Services.Specs;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets.Runway;

/// <summary>2026-10-13-8762v: the runway with what the worker speaks from and the page decides on.</summary>
/// <param name="NewestRunId">The newest code run that is not queued — the run a hold, refusal or close is keyed under.</param>
/// <param name="CutSpecs">The specs of the latest cut with specs.</param>
/// <param name="ChainLength">The attempts in the chain.</param>
public sealed record SeriesRunwayRead(
    TicketSeriesBudgetView View, string? NewestRunId, int SpecsLeft, int CutSpecs, int ChainLength, SeriesSettings Settings)
{
    /// <summary>The block the ticket page shows: only a real series — a cut larger than one attempt, or a chain of more
    /// than one attempt — so an ordinary ticket carries none.</summary>
    public TicketSeriesBudgetView? Page => CutSpecs > Settings.Series.SpecsPerAttempt || ChainLength > 1 ? View : null;
}
