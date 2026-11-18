using System.ComponentModel;
using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>2026-11-17-f3a8b: a run's event trail, filtered by type and step, page by page in sequence order.</summary>
public sealed class AdminEventTools(AgentSmithDbContext db, AdminReadSettings settings)
{
    [Description("Reads a run's events in sequence order: seq, type, step, time, role and the event's payload. Filter by "
        + "type (e.g. ToolResult, SandboxResult, LlmCallFinished, StepCompleted, RunFinished) or step index; pass the "
        + "last seq you saw as afterSeq for the next page.")]
    public async Task<string> ReadRunEvents(string runId, string? type = null, int? step = null, long? afterSeq = null,
        int? limit = null, CancellationToken cancellationToken = default)
    {
        var events = db.RunEvents.AsNoTracking().Where(e => e.RunId == runId);
        if (!string.IsNullOrWhiteSpace(type)) events = events.Where(e => e.Type == type);
        if (step is not null) events = events.Where(e => e.StepIndex == step);
        if (afterSeq is not null) events = events.Where(e => e.Seq > afterSeq);
        var page = await events.OrderBy(e => e.Seq).Take(settings.Rows(limit))
            .Select(e => new { e.Seq, e.Type, Step = e.StepIndex, e.Timestamp, e.Role, Payload = e.PayloadJson })
            .ToListAsync(cancellationToken);
        return AdminToolJson.Render(page);
    }
}
