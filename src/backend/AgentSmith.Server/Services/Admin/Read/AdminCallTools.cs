using System.ComponentModel;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>
/// 2026-11-17-f3a8b: where a run's model spend went — each call with its role, model, tokens, cache reads and writes,
/// cost and the prompt id it sent, and the totals per role and model. The prompt id lives only on the call's
/// LlmCallFinished event, joined by sequence; the totals are folded in memory, because sqlite cannot sum decimals.
/// </summary>
public sealed class AdminCallTools(AgentSmithDbContext db, AdminReadSettings settings)
{
    [Description("Reads a run's model calls: per call the step, role, model, tokens in and out, cached tokens read, "
        + "cache tokens written, cost in USD, duration and the prompt id (open it with read_prompt); plus totals per "
        + "role and per model. Pass the last call id you saw as afterId for the next page.")]
    public async Task<string> ReadLlmCalls(string runId, long? afterId = null, int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var all = await db.RunLlmCalls.AsNoTracking().Where(c => c.RunId == runId).OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var page = all.Where(c => afterId is null || c.Id > afterId).Take(settings.Rows(limit)).ToList();
        var prompts = await AdminPromptIds.OfAsync(db, runId, page.Select(c => c.EventSeq), cancellationToken);
        return AdminToolJson.Render(new
        {
            Calls = page.Select(c => Row(c, prompts)),
            ByRole = Totals(all, c => c.Role), ByModel = Totals(all, c => c.Model),
            Total = Totals(all, _ => "all").Single(),
        });
    }

    private static object Row(RunLlmCall c, IReadOnlyDictionary<long, string> prompts) => new
    {
        c.Id, c.StepIndex, c.SpecId, c.Role, c.Model, c.TokensIn, c.TokensOut, CacheRead = c.CachedTokensIn,
        CacheWrite = c.CacheCreationTokensIn, c.CostUsd, c.DurationMs,
        PromptId = c.EventSeq is { } seq ? prompts.GetValueOrDefault(seq) : null,
    };

    private static IEnumerable<object> Totals(IReadOnlyList<RunLlmCall> calls, Func<RunLlmCall, string?> key) =>
        calls.GroupBy(c => key(c) ?? "unknown").Select(g => new
        {
            Key = g.Key, Calls = g.Count(), TokensIn = g.Sum(c => c.TokensIn), TokensOut = g.Sum(c => c.TokensOut),
            CacheRead = g.Sum(c => c.CachedTokensIn), CacheWrite = g.Sum(c => c.CacheCreationTokensIn),
            CostUsd = g.Sum(c => c.CostUsd),
        });
}
