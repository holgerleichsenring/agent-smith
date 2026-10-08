using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-e8b9d: for each repo of the run that the previous attempt opened a pull request on, the
/// repo's provider reads the review; notes by untrusted people, bots and the host's system are dropped
/// (ours are kept for PrReviewSelection to judge), then the selection keeps what is feedback. A repo
/// the run does not carry, or a provider without a reader, is skipped.
/// <para>2026-10-08-e8b9c: the same pass reads a standing Request changes newer than the previous
/// attempt — trusted, not a bot — as the run's pull-request act.</para>
/// </summary>
public sealed class PrReviewFeedbackFetcher(
    ISourceProviderFactory sources, IPrReviewAuthorTrust trust, ILogger<PrReviewFeedbackFetcher> logger) : IPrReviewFeedbackFetcher
{
    private readonly PrStandingAct _standing = new(trust);

    public async Task FetchAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var attempt) || attempt is null) return;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r) ? r ?? [] : [];
        var feedback = new List<PrReviewFeedback>();
        foreach (var (repoName, url) in attempt.PullRequestUrls)
        {
            if (repos.FirstOrDefault(x => x.Name == repoName) is not { } repo) continue;
            var read = await ReadAsync(repo, url, attempt, pipeline, cancellationToken);
            if (read is { Count: > 0 }) feedback.Add(new PrReviewFeedback(repoName, url, read));
        }
        if (feedback.Count > 0) pipeline.Set(ContextKeys.PrReviewFeedback, (IReadOnlyList<PrReviewFeedback>)feedback);
    }

    private async Task<IReadOnlyList<PrReviewThread>?> ReadAsync(
        RepoConnection repo, string url, PreviousAttempt attempt, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var provider = sources.Create(repo);
        try
        {
            if (provider is IPrReviewActReader acts && await ActAsync(repo.Type, acts, url, attempt, cancellationToken) is { } act)
                ReworkActReader.ApplyPullRequest(pipeline, act);
            if (provider is not IPrReviewThreadReader reader) return null;
            var threads = await reader.ListAsync(url, cancellationToken);
            return PrReviewSelection.Select(await CountableAsync(repo.Type, threads, cancellationToken), attempt);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not read the review of {Url} — continuing without it", url);
            return null;
        }
    }

    private async Task<ReworkAct?> ActAsync(
        RepoType host, IPrReviewActReader reader, string url, PreviousAttempt attempt, CancellationToken cancellationToken) =>
        await _standing.NewestAsync(host, reader, url, attempt, cancellationToken) is { } note
            ? PrStandingAct.Act(note) : null;

    private async Task<IReadOnlyList<PrReviewThread>> CountableAsync(
        RepoType host, IReadOnlyList<PrReviewThread> threads, CancellationToken cancellationToken)
    {
        var verdicts = new Dictionary<string, bool>(StringComparer.Ordinal);
        var result = new List<PrReviewThread>();
        foreach (var thread in threads)
        {
            var notes = new List<PrReviewNote>();
            foreach (var note in thread.Notes)
                if (PrReviewSelection.IsOurs(note) || await IsPersonAsync(host, note, verdicts, cancellationToken)) notes.Add(note);
            result.Add(thread with { Notes = notes });
        }
        return result;
    }

    private async Task<bool> IsPersonAsync(
        RepoType host, PrReviewNote note, Dictionary<string, bool> verdicts, CancellationToken cancellationToken)
    {
        if (note.IsBot || note.IsSystem || note.Author is null) return false;
        if (!verdicts.TryGetValue(note.Author.AuthorId, out var trusted))
            verdicts[note.Author.AuthorId] = trusted = await trust.IsTrustedAsync(host, note.Author, cancellationToken);
        return trusted;
    }
}
