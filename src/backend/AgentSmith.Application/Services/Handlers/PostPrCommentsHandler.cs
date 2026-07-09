using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0167c: delivers the compiled review as PR comments via the repo's
/// <see cref="IPrCommentProvider"/>. Every body is stamped with an HTML
/// marker (<c>&lt;!-- agentsmith:pr-review:{file}:{line} --&gt;</c>, summary
/// uses <c>summary</c>) — a re-review on PR-synchronize first deletes every
/// marker-tagged comment from the previous run, then posts the new batch, so
/// the review is idempotent per file + line.
/// </summary>
public sealed class PostPrCommentsHandler(
    ISourceProviderFactory sourceFactory,
    ILogger<PostPrCommentsHandler> logger) : ICommandHandler<PostPrCommentsContext>
{
    internal const string MarkerPrefix = "<!-- agentsmith:pr-review:";

    public async Task<CommandResult> ExecuteAsync(
        PostPrCommentsContext context, CancellationToken cancellationToken)
    {
        if (sourceFactory.Create(context.Repo) is not IPrCommentProvider provider)
            return CommandResult.Fail(
                $"Repo '{context.Repo.Name}' ({context.Repo.Type}) has no PR-comment support — "
                + "pr-review requires a GitHub / GitLab / Azure DevOps repo.");

        var deleted = await provider.DeleteCommentsByMarkerAsync(
            context.PrNumber, MarkerPrefix, cancellationToken);
        await provider.PostReviewBatchAsync(
            context.PrNumber, Stamp(context.Review), cancellationToken);

        var message = $"Posted {context.Review.InlineComments.Count} inline comment(s) "
            + $"+ summary on PR #{context.PrNumber}"
            + (deleted > 0 ? $" (replaced {deleted} comment(s) from the previous review)" : "");
        logger.LogInformation("{Message}", message);
        return CommandResult.Ok(message);
    }

    private static PrReviewSummary Stamp(PrReviewSummary review) => new(
        $"{MarkerPrefix}summary -->\n{review.TopLevelComment}",
        review.InlineComments
            .Select(c => c with { Body = $"{MarkerPrefix}{c.File}:{c.StartLine} -->\n{c.Body}" })
            .ToList());
}
