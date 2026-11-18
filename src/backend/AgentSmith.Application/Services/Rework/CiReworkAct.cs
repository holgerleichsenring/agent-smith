using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-11-17-3435c: reads the ticket's pull-request records and answers <see cref="CiAct"/>'s act with where to answer
/// it — the first red pull request, so the existing 'started' notice goes there — and the evidence as its text.
/// </summary>
public sealed class CiReworkAct(IServiceScopeFactory scopes) : ICiReworkAct
{
    public async Task<PendingReworkAct?> NewestAsync(ResolvedProject project, string ticketId, PreviousAttempt since, CancellationToken ct)
    {
        if (project.Merge.When != MergeWhen.OnCiGreen) return null;
        IReadOnlyList<PrReviewRecordView> records;
        using (var scope = scopes.CreateScope())
            records = scope.ServiceProvider.GetService<IPrReviewRecordReader>() is { } reader
                ? await reader.ByTicketAsync(project.Name, ticketId, ct) : [];
        if (CiAct.Of(project, since, records) is not { } finding) return null;
        var first = finding.Red[0];
        return new PendingReworkAct(finding.Act, project.Repos.FirstOrDefault(r => r.Name == first.Repo), first.Url,
            CiEvidenceText.Of(CiEvidenceText.Feedback(finding)));
    }
}
