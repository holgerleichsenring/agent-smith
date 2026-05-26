using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: query-side helper for the GitHub provider. Issues one Octokit
/// request per label (Octokit ANDs the label list, so OR-semantics require
/// fan-out + in-memory dedupe) and maps each <see cref="Issue"/> via the
/// injected <see cref="ITicketFieldMapper{TRaw}"/>.
/// </summary>
internal sealed class GitHubIssueLister(
    GitHubClient client, string owner, string repo,
    ITicketFieldMapper<Issue> mapper, ILogger logger)
{
    public async Task<IReadOnlyList<Ticket>> ListByLabelsAsync(
        IReadOnlyCollection<string> labels, ItemStateFilter state,
        string descriptor, CancellationToken cancellationToken)
    {
        logger.LogInformation("GitHub List: repo={Owner}/{Repo} {Descriptor}", owner, repo, descriptor);
        try
        {
            var deduped = new Dictionary<int, Ticket>();
            foreach (var label in labels)
            {
                var req = new RepositoryIssueRequest { State = state };
                req.Labels.Add(label);
                var issues = await client.Issue.GetAllForRepository(owner, repo, req);
                foreach (var issue in issues)
                    deduped[issue.Number] = mapper.Map(new TicketId(issue.Number.ToString()), issue);
            }
            logger.LogInformation("GitHub List: returned {Count} ticket(s)", deduped.Count);
            return [.. deduped.Values];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GitHub List failed for {Owner}/{Repo} {Descriptor}", owner, repo, descriptor);
            return [];
        }
    }
}
