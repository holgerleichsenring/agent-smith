using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-27-3532b: whether a run's ticket is series-led — the newest series that holds it leads its run questions
/// (<see cref="SeriesLeads"/>: active, no series of one, a master that can act). Its run's question is then its series
/// master's: no answer box, tracker comment or deadline default answers it. Otherwise the ticket page answers it — a series
/// of one, whose only channel to a person that is, and a series whose master failed or spent its budget.
/// </summary>
public sealed class SeriesLedRun(ISeriesRunStore store, AgentSmithConfig config)
{
    /// <summary>The series that leads the ticket; null when none does.</summary>
    public async Task<SeriesRunRecord?> LeaderAsync(string project, string ticketId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(ticketId)) return null;
        var series = await store.ForTicketAsync(project, ticketId, ct);
        return Leads(series) ? series : null;
    }

    /// <summary>Whether this series leads its tickets' run questions.</summary>
    public bool Leads(SeriesRunRecord? series) => SeriesLeads.Questions(series, config.Dialogue.SeriesMasterBudgetUsd);

    /// <summary>A read of the series for one sweep: each project's series once, however many of its runs are parked.</summary>
    public SeriesLedScan Scan() => new(store);

    /// <summary>Why a person's answer to a series-led run is refused, naming the series that answers it.</summary>
    public static string Refusal(SeriesRunRecord series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return $"This run's question is its series master's: series {series.Id} answers it, and asks you in its conversation "
            + "when only you can decide. Answer there.";
    }
}
