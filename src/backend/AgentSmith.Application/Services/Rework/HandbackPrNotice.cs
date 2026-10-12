using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// The hand-back on every pull request the ticket's code runs opened (the previous attempt's
/// PullRequestUrls, on the repos this run carries), opened by our marker — so the admission never
/// reads it as a command and the review reader keeps it apart from what reviewers wrote.
/// </summary>
public sealed partial class HandbackPrNotice(ISourceProviderFactory sources, ILogger<HandbackPrNotice> logger) : IHandbackPrNotice
{
    public const string Marker = "<!-- agentsmith:handback -->";

    public async Task PostAsync(PipelineContext pipeline, string handback, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var attempt) || attempt is null) return;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r) ? r ?? [] : [];
        foreach (var (repoName, url) in attempt.PullRequestUrls)
            if (repos.FirstOrDefault(x => x.Name == repoName) is { } repo && Number().Match(url) is { Success: true } number)
                await SayAsync(repo, number.Groups[1].Value, $"{Marker}\n{handback}", cancellationToken);
    }

    private async Task SayAsync(RepoConnection repo, string number, string text, CancellationToken cancellationToken)
    {
        try
        {
            if (sources.Create(repo) is IPrCommentProvider comments) await comments.PostCommentAsync(number, text, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not post the hand-back on {Repo}#{Pr}", repo.Name, number);
        }
    }

    [GeneratedRegex(@"/(\d+)/?$")]
    private static partial Regex Number();
}
