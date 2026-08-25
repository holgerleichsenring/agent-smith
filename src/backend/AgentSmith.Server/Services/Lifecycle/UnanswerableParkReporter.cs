using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// 2026-08-25-a508: a question that cannot be answered is reported, not waited out.
/// <para>
/// A parked run is answerable through its checkpoint: the dashboard renders that row and the
/// answer endpoint delivers into the slot it names. A run that parked WITHOUT one — the
/// checkpoint write was skipped, or its event never reached the projection — shows the
/// operator a card with nothing to answer and waits for a manual status move that nothing
/// asks for. Silence is the failure mode, so the run is named on the findings channel the
/// installation already reports itself through.
/// </para>
/// <para>
/// The picture is republished every scan: a park that becomes answerable, resumes or is
/// cancelled stops being reported without anyone clearing it by hand.
/// </para>
/// </summary>
public sealed class UnanswerableParkReporter(
    IServiceScopeFactory scopeFactory,
    IRunCheckpointStore checkpoints,
    IStartupFindings findings,
    ILogger<UnanswerableParkReporter> logger)
{
    internal const string Subsystem = "parked-questions";

    /// <summary>Republishes the finding for every parked run that holds no answerable
    /// question. Returns how many were reported.</summary>
    public async Task<int> ReportAsync(CancellationToken cancellationToken)
    {
        var unanswerable = await UnanswerableAsync(cancellationToken);
        findings.Clear(Subsystem);
        foreach (var run in unanswerable) Record(run);
        return unanswerable.Count;
    }

    private async Task<IReadOnlyList<ParkedRun>> UnanswerableAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var parked = await scope.ServiceProvider
            .GetRequiredService<ParkedRunRepository>().ListAsync(cancellationToken);
        var unanswerable = new List<ParkedRun>();
        foreach (var run in parked)
            if (await checkpoints.GetByRunIdAsync(run.RunId, cancellationToken) is null)
                unanswerable.Add(run);
        return unanswerable;
    }

    private void Record(ParkedRun run)
    {
        logger.LogWarning(
            "Run {RunId} is parked on a question that cannot be answered — it has no checkpoint, "
            + "so the dashboard has nothing to render and no answer can be delivered to it",
            run.RunId);
        findings.Record(new StartupFinding(
            Subsystem, StartupFindingSeverity.Advisory,
            $"Run {run.RunId} is waiting for an answer that cannot be delivered: it parked on a "
            + $"question with no answerable slot. Answer it on ticket {run.TicketId} and move the "
            + "ticket back to a trigger status, or cancel the run.",
            Project: run.Project, Field: run.RunId));
    }
}
