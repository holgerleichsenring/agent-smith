using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Reads the head of the pull request a command was written on, through the configured repository's
/// provider. A repository no project declares, a provider without a head reader, or a failed read
/// leaves the command without a head — and then it starts nothing.
/// </summary>
public sealed class PrCommandHead(
    IPrCommentRepoLookup repos, ISourceProviderFactory sources, ILogger<PrCommandHead> logger) : IPrCommandHead
{
    public async Task<PrCommentCommand?> CompleteAsync(PrCommentCommand command, CancellationToken cancellationToken)
    {
        if (command.HeadRef is not null || !CommentCommandPrefix.IsCommand(command.Body)) return command;
        try
        {
            if (repos.Find(command.Author.RepositoryUrl) is not { } repo || sources.Create(repo.Repo) is not IPullRequestHeadReader reader)
                return null;
            var head = await reader.HeadAsync(command.PrNumber, cancellationToken);
            return command with { HeadRef = head.HeadRef, SameRepository = head.SameRepository, PrUrl = head.Url };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not read the head of {Pr}; the command starts nothing", command.PrLabel);
            return null;
        }
    }
}
