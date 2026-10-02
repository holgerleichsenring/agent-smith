using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: tests an unsaved tracker the way <see cref="ConnectionDraftCheckRunner"/>
/// tests a connection, then counts its open tickets when the type can — a type that cannot says
/// so instead of answering zero.
/// </summary>
public sealed class TrackerDraftCheckRunner(
    ICredentialResolver credentials, IEnumerable<ITrackerDraftCheck> checks,
    IEnumerable<ITicketCountCapability> counts, DraftCheckCollector collector) : ITrackerDraftCheckRunner
{
    public Task<DraftCheckReport> RunAsync(TrackerEntity draft, CancellationToken cancellationToken)
    {
        var tracker = Resolve(draft);
        var secret = tracker.Type == TrackerType.Jira && string.IsNullOrWhiteSpace(tracker.Email)
            ? new DraftCheckSecret(DraftCheckStep.Fail(DraftCheckStep.Secret,
                $"Tracker '{tracker.Name}' declares no email; Jira signs in with email and token."), null)
            : DraftCheckSecret.Resolve(tracker.Auth, () => credentials.For(tracker));
        if (secret.Token is not { } token) return Task.FromResult(new DraftCheckReport([secret.Step]));

        var check = checks.FirstOrDefault(c => c.Type == tracker.Type);
        if (check is null)
            return Task.FromResult(new DraftCheckReport([secret.Step,
                DraftCheckStep.Fail(DraftCheckStep.Host, $"No check exists for a {tracker.Type} tracker.")]));
        return collector.CollectAsync(secret.Step, ct => StepsAsync(check, tracker, token, ct), cancellationToken);
    }

    private async IAsyncEnumerable<DraftCheckStep> StepsAsync(
        ITrackerDraftCheck check, TrackerConnection tracker, string token,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var step in check.RunAsync(tracker, token, cancellationToken))
        {
            yield return step;
            if (!step.Ok) yield break;
        }
        var count = counts.FirstOrDefault(c => c.Type == tracker.Type);
        yield return count is null
            ? DraftCheckStep.Pass(DraftCheckStep.Tickets, "Count not supported by this tracker.")
            : await count.CountOpenAsync(tracker, token, cancellationToken);
    }

    private static TrackerConnection Resolve(TrackerEntity draft)
    {
        var id = string.IsNullOrWhiteSpace(draft.Id) ? "draft" : draft.Id;
        var raw = new Dictionary<string, RawTrackerEntry> { [id] = RawConfigPatch.Tracker(draft, existing: null) };
        return new TrackerCatalogBuilder().Build(raw, [], [])[id];
    }
}
