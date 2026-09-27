using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-25-8e51e: rewrites the framework's region of a GitHub issue body.
/// <para>
/// GitHub stores an issue body as the markdown it was given and hands back the same bytes, so the
/// region the filing wrote is found in the body exactly as it was written and everything outside
/// the markers survives byte for byte. That round trip is the whole reason this tracker can carry
/// a rewrite and Jira cannot.
/// </para>
/// <para>
/// Its own class beside <see cref="GitHubTicketProvider"/>, which is over the file-length
/// baseline and may only get shorter. It builds its own client from the same connection, the way
/// the provider does.
/// </para>
/// </summary>
public sealed class GitHubTicketRewriter : ITicketRewriter
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly GitHubClient _client;
    private readonly ILogger _logger;

    public GitHubTicketRewriter(GitHubTicketConnection connection, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var segments = new Uri(connection.RepoUrl).AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2)
            throw new Domain.Exceptions.ConfigurationException($"Invalid GitHub URL: {connection.RepoUrl}");
        (_owner, _repo) = (segments[0], segments[1]);
        _client = new GitHubClient(new ProductHeaderValue("AgentSmith"))
        { Credentials = new Credentials(connection.Token) };
        _logger = logger;
    }

    public async Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketId);
        if (!int.TryParse(ticketId.Value, out var number))
            return TicketRewriteResult.Failed($"'{ticketId.Value}' is not a GitHub issue number.");
        try
        {
            var body = (await _client.Issue.Get(_owner, _repo, number)).Body;
            if (FramedTicketRegion.Replace(body, region) is not { } rewritten)
                return TicketRewriteResult.Unsupported(TicketRegionRefusal.NoRegion);
            _logger.LogInformation(
                "TICKET WRITE #{Ticket}: issue body region <- {Caller}",
                ticketId.Value, TicketWriteAudit.Caller());
            await _client.Issue.Update(_owner, _repo, number, new IssueUpdate { Body = rewritten });
            return TicketRewriteResult.Ok;
        }
        // A tracker that refused or could not be reached is an ANSWER here: the conversation
        // that asked for the amendment has to be told, and a thrown turn tells it nothing.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Rewriting the region of GitHub issue {Ticket} failed", ticketId.Value);
            return TicketRewriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }
}
