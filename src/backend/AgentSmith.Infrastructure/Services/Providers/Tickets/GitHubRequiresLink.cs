using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-14-dbcem: GitHub's own "blocked by" first — the dependency endpoint github.com and GHES 3.20 and later
/// document, naming the blocking issue by its internal id, not its number. Octokit 14 types no such call, so it goes
/// through the client's connection. A GitHub that refuses the API (an older GHES, the feature off, a missing
/// permission) gets one comment on the dependent instead — a mention GitHub shows in the timeline, never as blocked.
/// </summary>
public sealed class GitHubRequiresLink : ITicketRequiresLink
{
    internal const string Accepts = "application/vnd.github+json";
    internal const string NoDependencyApi = "the dependency API is not available here";

    private readonly IGitHubClient _client;
    private readonly string _owner;
    private readonly string _repo;
    private readonly ILogger _logger;

    public GitHubRequiresLink(IGitHubClient client, GitHubTicketConnection connection, ILogger logger)
    {
        _client = client;
        var address = GitHubRepoAddress.Parse(connection.RepoUrl);
        (_owner, _repo) = (address.Owner, address.Repo);
        _logger = logger;
    }

    public async Task<RequiresLinkResult> LinkAsync(TicketId dependent, TicketId required, CancellationToken cancellationToken)
    {
        if (!int.TryParse(dependent.Value, out var dependentNumber) || !int.TryParse(required.Value, out var requiredNumber))
            return RequiresLinkResult.Refused("not a GitHub issue number");
        try
        {
            var blocking = await _client.Issue.Get(_owner, _repo, requiredNumber);
            var status = await _client.Connection.Post(BlockedBy(_owner, _repo, dependentNumber),
                new Dictionary<string, object> { ["issue_id"] = blocking.Id }, Accepts, cancellationToken);
            if ((int)status is >= 200 and < 300) return RequiresLinkResult.Done;
            _logger.LogWarning("GitHub answered {Status} to blocking issue {Dependent} on {Required}", status, dependentNumber, requiredNumber);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "GitHub refused the dependency of issue {Dependent} on {Required}; commenting instead", dependentNumber, requiredNumber);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "GitHub did not answer the dependency of issue {Dependent} on {Required}", dependentNumber, requiredNumber);
            return RequiresLinkResult.Unreached(RequiresLinkRefusal.NoAnswer);
        }
        return await CommentAsync(dependentNumber, requiredNumber, cancellationToken);
    }

    internal static string Comment(int required) => $"Agent Smith — requires #{required} (must merge first)";

    internal static Uri BlockedBy(string owner, string repo, int dependent) =>
        new($"repos/{owner}/{repo}/issues/{dependent}/dependencies/blocked_by", UriKind.Relative);

    private async Task<RequiresLinkResult> CommentAsync(int dependent, int required, CancellationToken cancellationToken)
    {
        try
        {
            await _client.Issue.Comment.Create(_owner, _repo, dependent, Comment(required));
            return RequiresLinkResult.Done;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "The requires comment on GitHub issue {Dependent} could not be written", dependent);
            var refusal = RequiresLinkRefusal.Of(ex);
            return refusal.Unreachable ? refusal : RequiresLinkResult.Refused($"{NoDependencyApi}, and the comment: {refusal.Reason}");
        }
    }
}
