using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: walks a check's steps under the overall budget and stops at the first failure,
/// so no call after a failing step is made. A budget that runs out ends the report with a step
/// saying so; a caller that goes away gets its cancellation.
/// </summary>
public sealed class DraftCheckCollector
{
    public static readonly TimeSpan OverallBudget = TimeSpan.FromSeconds(30);

    public async Task<DraftCheckReport> CollectAsync(
        DraftCheckStep secret, Func<CancellationToken, IAsyncEnumerable<DraftCheckStep>> steps,
        CancellationToken cancellationToken, TimeSpan? budget = null)
    {
        List<DraftCheckStep> report = [secret];
        var limit = budget ?? OverallBudget;
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overall.CancelAfter(limit);
        try
        {
            await foreach (var step in steps(overall.Token).WithCancellation(overall.Token))
            {
                report.Add(step);
                if (!step.Ok) break;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            report.Add(DraftCheckStep.Fail(DraftCheckStep.Time,
                $"The check stopped after {limit.TotalSeconds:0} s."));
        }
        return new DraftCheckReport(report);
    }
}
