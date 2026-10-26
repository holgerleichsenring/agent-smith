using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Delivery;

/// <summary>
/// 2026-10-25-0099b: GET /api/delivery's answer. The attempts of the previous and the current period are read once,
/// filtered to the caller's projects in the query and bounded by <see cref="Ceiling"/>; their specs and changed
/// repositories are read for the attempts kept. The ticket list's own scoped read gives each ticket's bucket and its
/// pull-request records, so a row never disagrees with the Tickets page. Every span is then counted in memory.
/// <para>2026-10-26-7000c: the window's spec derivations are read once beside them, scoped and bounded alike, and
/// each span's are counted in its cost and agent time, never as attempts.</para>
/// </summary>
public sealed class DeliveryReader(
    DeliveryAttemptRepository attemptRows,
    DeliveryWorkRepository work,
    TicketListReader ticketList,
    DeliveryAttemptOutcome outcomes,
    DeliveryTicketComposer composer,
    DeliveryFiguresCalculator figures,
    DeliveryProjectRows projects,
    DeliveryEndCauseCounter causes,
    DeliveryTicketViews views,
    TimeProvider clock)
{
    /// <summary>The attempts one answer reads at most; the answer says when it bit.</summary>
    public const int Ceiling = 5000;

    public async Task<DeliveryView> ReadAsync(
        DeliveryWindow window, ProjectScope scope, IReadOnlyList<string> chips, CancellationToken ct)
    {
        var (attempts, truncated) = await AttemptsAsync(window, scope, ct);
        var (derivations, derivationsTruncated) = await DerivationsAsync(window, scope, ct);
        var list = await ticketList.AllAsync(scope, ct);
        IReadOnlyList<DeliveryTicket> In(DeliverySpan span) => composer.Compose(attempts, span, list.PrRecords);
        IReadOnlyList<DeliveryDerivation> DerivedIn(DeliverySpan span) => [.. derivations.Where(d => span.Holds(d.RunId))];
        var (current, derived) = (In(window.Current), DerivedIn(window.Current));
        var buckets = list.Runs.ToDictionary(t => (t.Summary.Project, t.Summary.TicketId), t => t.Summary.Bucket);
        return new DeliveryView(
            window.Period, window.Current.Start, window.Current.End, chips,
            figures.Of(current, derived), figures.Of(In(window.Previous), DerivedIn(window.Previous)),
            [.. window.Slots.Select(s => figures.Of(In(s), DerivedIn(s)))], causes.Of(current), projects.Of(current, derived),
            views.Of(current, derived, buckets, clock.GetUtcNow()), truncated || derivationsTruncated);
    }

    private async Task<(IReadOnlyList<DeliveryDerivation> Derivations, bool Truncated)> DerivationsAsync(
        DeliveryWindow window, ProjectScope scope, CancellationToken ct)
    {
        var read = await attemptRows.DerivationsAsync(scope, window.Previous.FromKey, window.Current.ToKey, Ceiling, ct);
        var now = clock.GetUtcNow();
        return ([.. read.Rows.Select(r => outcomes.Derivation(r, now))], read.Truncated);
    }

    private async Task<(IReadOnlyList<DeliveryAttempt> Attempts, bool Truncated)> AttemptsAsync(
        DeliveryWindow window, ProjectScope scope, CancellationToken ct)
    {
        var (from, to) = (window.Previous.FromKey, window.Current.ToKey);
        var read = await attemptRows.ReadAsync(scope, from, to, Ceiling, ct);
        // The ceiling drops the oldest attempts; the children are read for the attempts kept.
        var floor = read.Truncated && read.Rows.Count > 0 ? read.Rows[^1].RunId : from;
        var specs = (await work.SpecsAsync(scope, floor, to, ct)).ToLookup(s => s.RunId, StringComparer.Ordinal);
        var repos = (await work.ReposAsync(scope, floor, to, ct)).ToLookup(r => r.RunId, r => r.RepoName, StringComparer.Ordinal);
        var now = clock.GetUtcNow();
        return ([.. read.Rows.Select(r => outcomes.Of(r, [.. specs[r.RunId]], [.. repos[r.RunId].Distinct()], now))], read.Truncated);
    }
}
