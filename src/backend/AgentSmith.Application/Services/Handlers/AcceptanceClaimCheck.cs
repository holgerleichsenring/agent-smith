using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-17-a196c: the met claims of a verdict whose citations resolve against nothing — one line each,
/// the criterion and the resolver's own note. It resolves; it does not judge whether a cited file MEANS
/// the criterion, which stays the keystone's question. A met claim without a citations key cites nothing
/// (2026-10-17-1170: the pinned coding master is told to cite, so the old verdict shape is no longer exempt).
/// </summary>
public sealed class AcceptanceClaimCheck
{
    public IReadOnlyList<string> Unresolved(
        MasterVerification? verification, IReadOnlyList<string> criteria, CitationResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(resolver);
        if (verification?.AcceptanceDispositions is not { } dispositions)
            return [];
        return [.. AcceptancePairing.Pair(criteria, dispositions)
            .Where(p => p.Disposition is { Status: AcceptanceStatus.Met })
            .Select(p => resolver.Resolve(new AccountRow(
                p.Criterion, AccountDisposition.Satisfied, Citations: p.Disposition!.Citations ?? [])))
            .Where(row => row.IsOutstanding)
            .Select(row => $"{row.Criterion}: {row.Note}")];
    }
}
