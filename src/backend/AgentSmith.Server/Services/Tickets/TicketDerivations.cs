using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-26-bdd0f: the spec derivations among a ticket's runs, newest first, read in one scope — the newest cut
/// of each (its specs and hand-back case) and the repository rows that carry a pull request. Nothing new is
/// persisted for them: the run's own summary says where the specs are or why it stopped.
/// </summary>
public sealed class TicketDerivations(IServiceScopeFactory scopes)
{
    public async Task<IReadOnlyList<TicketDerivationView>> OfAsync(IEnumerable<Run> runs, CancellationToken ct)
    {
        var derivations = runs.Where(r => r.Pipeline == PipelinePresets.SpecDeriveName)
            .OrderByDescending(r => r.Id, StringComparer.Ordinal).ToList();
        if (derivations.Count == 0) return [];
        var ids = derivations.Select(r => r.Id).ToList();
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var cuts = (await uow.Set<RunSeriesCut>().AsNoTracking().Where(c => ids.Contains(c.RunId)).OrderBy(c => c.Id)
            .ToListAsync(ct)).GroupBy(c => c.RunId).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var prs = (await uow.Set<RunRepo>().AsNoTracking().Where(r => ids.Contains(r.RunId) && r.PrUrl != null)
            .ToListAsync(ct)).ToLookup(r => r.RunId, r => r.PrUrl!, StringComparer.Ordinal);
        return [.. derivations.Select(run => View(run, cuts.GetValueOrDefault(run.Id), [.. prs[run.Id].Distinct()]))];
    }

    private static TicketDerivationView View(Run run, RunSeriesCut? cut, IReadOnlyList<string> prs)
    {
        var specs = cut is null ? 0 : SpecCount(cut.SpecsJson);
        return new(run.Id, run.Status, run.StartedAt, run.FinishedAt, run.CostTotalUsd,
            Outcome(run, cut, specs), specs, prs, run.Summary);
    }

    private static string Outcome(Run run, RunSeriesCut? cut, int specs) =>
        run.FinishedAt is null || RunStatuses.IsWaiting(run.Status) ? TicketDerivationOutcomes.Running
        : run.Status != RunStatuses.Success ? TicketDerivationOutcomes.NotFinished
        : cut?.HandbackCase is not null ? TicketDerivationOutcomes.Asked
        : specs > 0 ? TicketDerivationOutcomes.Derived
        : TicketDerivationOutcomes.NotFinished;

    private static int SpecCount(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        return document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.GetArrayLength() : 0;
    }
}
