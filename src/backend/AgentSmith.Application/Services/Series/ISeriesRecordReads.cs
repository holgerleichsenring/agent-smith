using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207e: the curated record reads the admin assistant has (a run, its model calls, its events),
/// lent to the series master so it can see why a ticket's run ended without a reader of its own. The server supplies
/// them; a composition without them gives none.</summary>
public interface ISeriesRecordReads
{
    IList<AITool> Tools();
}
