using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: files a plan in order and settles what was created. Before the first ticket, a failure is the
/// filing's error with nothing created and the caller's cancellation escapes. Once a ticket exists the filing never
/// fails: a later error or a cancellation stops the loop, the report names what was not created, and every created
/// ticket is still settled through <see cref="TicketGraphStarts"/> on no token — complete with nothing unmet starts,
/// the others are held naming what they wait for, or reported not started — because a root nobody starts never
/// wakes its dependents.
/// </summary>
public sealed class FilingPlanFiler(
    ITicketProviderFactory ticketFactory, PlannedTicketFiler tickets, TicketGraphStarts starts,
    ILogger<FilingPlanFiler> logger)
{
    public async Task<FilingReport> FileAsync(
        FilingRequest request, ResolvedProject project, FilingPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(plan);
        var progress = new FilingProgress();
        ITicketProvider? provider = null;
        string? error = null;
        try
        {
            provider = ticketFactory.Create(project.Tracker);
            await CreateAllAsync(provider, request, project, plan, progress, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested || progress.Tickets.Count > 0)
        {
            error = Stopped(ex, ct, progress, plan);
            logger.LogError(ex, "Filing for conversation {Conversation} stopped after {Count} ticket(s)", request.Conversation, progress.Filed.Count);
        }
        if (provider is not null && progress.Tickets.Count > 0)
            progress.Notes.AddRange(await starts.SettleAsync(provider, project, progress.Tickets, request.MayStartRuns, progress.Filed));
        return new FilingReport(progress.Filed, error) { Notes = progress.Notes };
    }

    // Between tickets the run's own cancel still stops the loop; once a ticket exists, no call runs on the token.
    private async Task CreateAllAsync(
        ITicketProvider provider, FilingRequest request, ResolvedProject project, FilingPlan plan, FilingProgress progress,
        CancellationToken ct)
    {
        foreach (var planned in plan.Tickets)
        {
            if (progress.Tickets.Count > 0) ct.ThrowIfCancellationRequested();
            await tickets.FileAsync(provider, request, project, planned, progress, progress.Tickets.Count > 0 ? CancellationToken.None : ct);
        }
    }

    private static string Stopped(Exception ex, CancellationToken ct, FilingProgress progress, FilingPlan plan)
    {
        if (plan.Tickets.Where(t => !progress.Ids.ContainsKey(t.Key)).ToList() is { Count: > 0 } rest && progress.Filed.Count > 0)
            progress.Notes.Add($"Not created: {string.Join(", ", rest.Select(t => $"{t.Key} — {t.Goal}"))}.");
        return ex is OperationCanceledException && ct.IsCancellationRequested
            ? $"The filing was interrupted after {progress.Filed.Count} of {plan.Tickets.Count} tickets." : ex.Message;
    }
}
