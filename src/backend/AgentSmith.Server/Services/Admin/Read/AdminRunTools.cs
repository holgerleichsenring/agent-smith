using System.ComponentModel;
using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>
/// 2026-11-17-f3a8b: the runs — a page of them, newest first, and one run with its steps, decisions and how it ended.
/// Ordered by id, which sorts by start time; sqlite cannot order a DateTimeOffset in SQL.
/// </summary>
public sealed class AdminRunTools(AgentSmithDbContext db, AdminReadSettings settings)
{
    [Description("Lists runs newest first: id, project, pipeline, ticket, status, how it ended, cost and tokens. "
        + "Filter by project or status; pass the last id you saw as beforeId for the next page.")]
    public async Task<string> ListRuns(string? project = null, string? status = null, string? beforeId = null,
        int? limit = null, CancellationToken cancellationToken = default)
    {
        var runs = db.Runs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(project)) runs = runs.Where(r => r.Project == project);
        if (!string.IsNullOrWhiteSpace(status)) runs = runs.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(beforeId)) runs = runs.Where(r => string.Compare(r.Id, beforeId) < 0);
        var page = await runs.OrderByDescending(r => r.Id).Take(settings.Rows(limit)).Select(r => new
        {
            r.Id, r.Project, r.Pipeline, r.TicketId, r.Status, r.EndKind, r.StartedAt, r.FinishedAt,
            r.CostTotalUsd, r.TokensIn, r.TokensOut, r.Summary,
        }).ToListAsync(cancellationToken);
        return AdminToolJson.Render(page);
    }

    [Description("Reads one run: its status, how and why it ended (end kind and detail), cost and tokens, every step "
        + "with its status and result, and the decisions it recorded.")]
    public async Task<string> ReadRun(string runId, CancellationToken cancellationToken = default)
    {
        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null) return $"No run with id '{runId}'.";
        var steps = await db.RunSteps.AsNoTracking().Where(s => s.RunId == runId).OrderBy(s => s.StepIndex)
            .Select(s => new { s.StepIndex, s.CommandName, s.SpecId, s.Status, s.DurationSeconds, s.ResultMessage, s.LlmMs })
            .ToListAsync(cancellationToken);
        var decisions = await db.RunDecisions.AsNoTracking().Where(d => d.RunId == runId).OrderBy(d => d.Id)
            .Select(d => new { d.StepIndex, d.Category, d.Name, d.Reason }).ToListAsync(cancellationToken);
        return AdminToolJson.Render(new
        {
            run.Id, run.Project, run.Pipeline, run.TicketId, run.Status, run.EndKind, EndDetail = run.EndDetailJson,
            run.Summary, run.StartedAt, run.FinishedAt, run.CostTotalUsd, run.TokensIn, run.TokensOut, run.BudgetCapUsd,
            Steps = steps, Decisions = decisions,
        });
    }
}
