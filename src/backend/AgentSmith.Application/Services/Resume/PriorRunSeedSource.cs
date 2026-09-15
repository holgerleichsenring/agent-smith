using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0356: the same-ticket RESUME seed — the checklist a new run opens on when a
/// prior run of the same work left a persisted ledger behind (mid-run flushes
/// make a reaped run's ledger durable). Reading it, gating it and logging what
/// was carried over is one responsibility, held here rather than in the master
/// handler.
/// <para>2026-09-09-b26f: the seed is keyed on PROJECT AND TICKET. A tracker id
/// is unique only within its project, so two projects on one tracker can both
/// carry the same id; on the ticket alone the newer run opens on the other
/// project's checklist. No resolved project name (ephemeral CLI compositions)
/// means no seed — an unkeyed read is exactly the defect.</para>
/// <para>A read failure degrades to the empty seed: resume is an affordance,
/// never a blocker.</para>
/// </summary>
public sealed class PriorRunSeedSource(
    IPriorRunLedgerReader reader,
    ILogger<PriorRunSeedSource> logger)
{
    public async Task<IReadOnlyList<ProgressLedgerEntry>> SeedAsync(
        PipelineContext pipeline, string ticketId, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<string>(ContextKeys.ProjectName, out var project)
            || string.IsNullOrWhiteSpace(project))
            return Array.Empty<ProgressLedgerEntry>();
        try
        {
            var prior = await reader.ReadLatestForTicketAsync(project!, ticketId, cancellationToken);
            var seed = PriorRunLedgerSeeder.Seed(prior, DateTimeOffset.UtcNow);
            if (seed.Count > 0)
                logger.LogInformation(
                    "Seeded the progress ledger from prior run {PriorRunId} ({Count} item(s), "
                    + "resume of ticket {TicketId} in project {Project})",
                    prior!.RunId, seed.Count, ticketId, project);
            return seed;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Prior-run ledger read failed — starting with an empty ledger");
            return Array.Empty<ProgressLedgerEntry>();
        }
    }
}
