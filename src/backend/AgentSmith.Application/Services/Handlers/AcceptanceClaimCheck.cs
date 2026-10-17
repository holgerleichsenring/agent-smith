using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-17-a196c: the met claims of a verdict whose citations resolve against nothing — one line each,
/// the criterion and the resolver's own note. It resolves; it does not judge whether a cited file MEANS
/// the criterion, which stays the keystone's question. A verdict in which no disposition carries a
/// citations key predates them and is not checked; once one does, a met claim without the key cites nothing.
/// </summary>
public sealed class AcceptanceClaimCheck
{
    public IReadOnlyList<string> Unresolved(
        MasterVerification? verification, IReadOnlyList<string> criteria, CitationResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(resolver);
        if (verification?.AcceptanceDispositions is not { } dispositions || dispositions.All(d => d.Citations is null))
            return [];
        return [.. AcceptancePairing.Pair(criteria, dispositions)
            .Where(p => p.Disposition is { Status: AcceptanceStatus.Met })
            .Select(p => resolver.Resolve(new AccountRow(
                p.Criterion, AccountDisposition.Satisfied, Citations: p.Disposition!.Citations ?? [])))
            .Where(row => row.IsOutstanding)
            .Select(row => $"{row.Criterion}: {row.Note}")];
    }
}
