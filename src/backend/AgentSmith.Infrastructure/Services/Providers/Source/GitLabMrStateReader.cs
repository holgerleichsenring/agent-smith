using System.Globalization;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-13-9a59b: a GitLab merge request's state and standing decisions. GitLab writes a system
/// note for every approval, every request for changes and every withdrawn approval; per author the
/// newest of them decides, and "unapproved" decides nothing. The merge request's author, the
/// token's own user and bots are excluded. The decision's host id is the system note's id, which
/// the thread reader returns as a note.
/// </summary>
public sealed class GitLabMrStateReader(
    string baseUrl, string projectPath, string token, string repoUrl, HttpClient http, ILogger logger) : IPrReviewStateReader
{
    public const string Unapproved = "unapproved this merge request";
    private const int MaxPages = 10;
    private readonly GitLabNotesPager _notes = new(baseUrl, projectPath, token, http, logger);
    private readonly GitLabUsers _users = new(baseUrl, token, http);
    private readonly GitLabMergeRequestRead _mergeRequests = new(baseUrl, projectPath, token, http);

    public async Task<PrReviewState> ReadAsync(string prUrl, CancellationToken cancellationToken)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            throw new ArgumentException($"Not a GitLab merge request URL: {prUrl}", nameof(prUrl));
        var mr = await _mergeRequests.ReadAsync(iid, cancellationToken);
        var excluded = new[] { mr.AuthorId, await _users.TokenUserIdAsync(cancellationToken) };
        var newest = (await _notes.ReadAsync(iid.ToString(CultureInfo.InvariantCulture), MaxPages, cancellationToken))
            .Where(n => n.System && n.AuthorId is not null && n.Body is GitLabMrActReader.RequestedChanges or GitLabMrActReader.Approved or Unapproved)
            .GroupBy(n => n.AuthorId!.Value)
            .Select(g => g.MaxBy(n => n.CreatedAt)!)
            .Where(n => n.Body != Unapproved && !excluded.Contains(n.AuthorId));
        var decisions = new List<PrReviewDecision>();
        foreach (var note in newest)
            if (!await _users.IsBotAsync(note.AuthorId!.Value, cancellationToken))
                decisions.Add(Decision(note));
        var (state, at) = State(mr);
        return new PrReviewState(iid.ToString(CultureInfo.InvariantCulture), state, at, decisions, mr.Sha);
    }

    private PrReviewDecision Decision(GitLabNote note)
    {
        var id = note.AuthorId!.Value.ToString(CultureInfo.InvariantCulture);
        return new PrReviewDecision(note.Id.ToString(CultureInfo.InvariantCulture),
            new PrCommentAuthor(repoUrl, Uri.UnescapeDataString(projectPath), id, note.AuthorUsername ?? id), false,
            note.Body == GitLabMrActReader.Approved ? PrReviewDecisions.Approved : PrReviewDecisions.ChangesRequested,
            note.CreatedAt, note.Body);
    }

    private static (string State, DateTimeOffset At) State(GitLabMergeRequest mr) => mr.State switch
    {
        "merged" => (PrReviewStates.Merged, mr.MergedAt ?? mr.ClosedAt ?? mr.CreatedAt ?? DateTimeOffset.MinValue),
        "closed" => (PrReviewStates.Closed, mr.ClosedAt ?? mr.CreatedAt ?? DateTimeOffset.MinValue),
        _ => (PrReviewStates.Open, mr.CreatedAt ?? DateTimeOffset.MinValue),
    };
}
