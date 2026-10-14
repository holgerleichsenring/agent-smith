using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9c: for a pull-request act on a full set, the ticket is told by the derivation comment
/// and the pull request by this marked comment, on every open pull request of the previous attempt.
/// </summary>
public sealed partial class FullSetPrNotice(
    ISourceProviderFactory sources, Specs.ISeriesSettingsReader series, ILogger<FullSetPrNotice> logger) : IFullSetPrNotice
{
    public const string Marker = "<!-- agentsmith:rework -->";

    public async Task PostAsync(PipelineContext pipeline, SpecSet set, CancellationToken cancellationToken)
    {
        var cap = series.Current().Series.MaxSpecs; // 2026-10-13-8762u
        if (set.UnexecutedTail.Count > 0 || set.Executed.Count < cap) return;
        if (!pipeline.TryGet<ReworkAct>(ContextKeys.ReworkAct, out var act) || act?.Channel != ReworkChannel.PullRequest) return;
        if (!pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var attempt) || attempt is null) return;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r) ? r ?? [] : [];
        foreach (var (repoName, url) in attempt.PullRequestUrls)
            if (repos.FirstOrDefault(x => x.Name == repoName) is { } repo && Number().Match(url) is { Success: true } number)
                await SayAsync(repo, number.Groups[1].Value, cap, cancellationToken);
    }

    private async Task SayAsync(RepoConnection repo, string number, int cap, CancellationToken cancellationToken)
    {
        try
        {
            if (sources.Create(repo) is IPrCommentProvider comments)
                await comments.PostCommentAsync(number, $"{Marker}\nAgent Smith — all {cap} specs of this "
                    + "ticket's series have run, the most one series can hold, so this review was not cut into it. "
                    + "File the feedback as a new ticket.", cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not tell {Repo}#{Pr} that the specification is full", repo.Name, number);
        }
    }

    [GeneratedRegex(@"/(\d+)/?$")]
    private static partial Regex Number();
}
