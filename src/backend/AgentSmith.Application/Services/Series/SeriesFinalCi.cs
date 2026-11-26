using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-9992e: the series master's CI act on the series' final pull requests (ci "final"): read asks each host for the
/// CI on the pull request's head now; requeue queues its required builds again; start starts its pull-request CI. Each
/// answers per repository; a host without the API says so.
/// </summary>
public sealed class SeriesFinalCi(ISourceProviderFactory sources, PullRequestBuildRequeue requeue, IPullRequestCiTriggerFactory? triggers = null)
{
    public async Task<string> ActAsync(SeriesMasterContext context, SeriesRunRecord record, string act, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (SeriesFinalMerge.Finals(context.Project, record) is not { Count: > 0 } finals) return SeriesFinalMerge.NoFinal(record);
        var said = new List<string>();
        foreach (var (repo, delivery) in finals)
            said.Add($"{repo.Name} ({delivery.Url}): " + act switch
            {
                SeriesCiActs.Read => await ReadAsync(repo, delivery.Url, context.Project.Merge, ct),
                SeriesCiActs.Requeue => $"{await requeue.RequeueAsync(repo, delivery.Url, ct, headSettled: true)} required build(s) queued again.",
                SeriesCiActs.Start => await StartAsync(repo, delivery.Url, ct),
                _ => SeriesCiActs.Unknown(act),
            });
        return string.Join(" ", said);
    }

    private async Task<string> ReadAsync(RepoConnection repo, string url, MergePolicyConfig policy, CancellationToken ct)
    {
        if (sources.Create(repo) is not IPullRequestCiReader reader) return "its host reads no CI.";
        var ci = await reader.ReadCiAsync(url, new CiReadRequest(null, policy.CiExcerptChars, policy.CiFailedChecksRead), ct);
        // 2026-11-26-49d3a: a red head answers its failed checks with their evidence.
        var failed = ci.State == CiStates.Red && ci.Failures.Count > 0
            ? "\n\n" + Rework.CiEvidenceText.Of([new CiFailureFeedback(repo.Name, url, ci.Failures)]) : string.Empty;
        return $"CI {ci.State} on head {Short(ci.HeadSha)}.{failed}";
    }

    private async Task<string> StartAsync(RepoConnection repo, string url, CancellationToken ct)
    {
        if (triggers?.Create(repo) is not { } trigger) return "its host has no API that starts a pull request's CI.";
        var answer = await trigger.StartAsync(url, repo.DefaultBranch, ct);
        return $"{answer.Outcome}: {answer.Text}";
    }

    private static string Short(string? sha) => sha is { Length: > 7 } ? sha[..7] : sha ?? "unknown";
}
