using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79a: what a cut whose ACCOUNTING is incomplete becomes — the refusal is
/// reported and the run falls back to one phase carrying the whole ticket.
/// <para>
/// It left DeriveSpecHandler because reporting an incomplete accounting and building the
/// fallback from the refused cut is its own responsibility: the cut was refused for its
/// COVERAGE, not for its criteria, so the done-list is carried forward and the run's acceptance
/// contract stays non-empty without one being invented.
/// </para>
/// </summary>
public sealed class SpecCoverageRefusal(SpecCutGate gate, SpecFallback fallback)
{
    public async Task<SpecSet> ApplyAsync(
        PipelineContext pipeline, Ticket ticket, string key,
        IReadOnlyList<TicketSegment> segments, SpecSet refused, SpecSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(refused);
        await gate.RefusedAsync(
            pipeline, ticket.Id.Value,
            "segment(s) " + string.Join(", ", refused.Accounting.Unaccounted)
            + " were neither carried by a phase nor discarded with a reason", cancellationToken);
        return fallback.Build(
            key, ticket, segments,
            [.. refused.Phases.SelectMany(p => p.Draft.Done).Distinct(StringComparer.Ordinal)],
            source);
    }
}
