using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Decides what a PR/MR comment starts, the same way on every code host: the structural
/// command match first (an ordinary comment costs nothing), then the author's write access
/// on the repository (an untrusted command costs no model call), then the model resolves
/// the command to a pipeline, which must be one <see cref="PrCommentPipelines.Allowed"/> names.
/// </summary>
public sealed class PrCommentCommandAdmission(
    CommentIntentParser commentIntentParser,
    ServerContext serverContext,
    ILogger<PrCommentCommandAdmission> logger)
{
    public async Task<WebhookResult> AdmitAsync(
        PrCommentCommand command, IPrCommentAuthorTrust trust, CancellationToken cancellationToken)
    {
        var match = commentIntentParser.Match(command.Body);
        if (match.Type == CommentIntentType.Unknown)
            return WebhookResult.NotHandled();
        if (match.Type == CommentIntentType.Help)
        {
            logger.LogInformation("PR comment help request from {Author} on {Pr}",
                command.Author.AuthorLogin, command.PrLabel);
            return WebhookResult.NotHandled();
        }

        if (!await trust.IsTrustedAsync(command.Author, cancellationToken))
            return Refuse(command);

        var request = await commentIntentParser.ResolveAsync(
            match.Tail!, serverContext.ConfigPath, cancellationToken);
        return Route(command, request);
    }

    private WebhookResult Refuse(PrCommentCommand command)
    {
        logger.LogInformation(
            "Ignoring PR comment command from {Author} on {Pr}: the author may not write to the repository",
            command.Author.AuthorLogin, command.PrLabel);
        return WebhookResult.NotHandled("comment author may not write to the repository");
    }

    private WebhookResult Route(PrCommentCommand command, PipelineRequest request)
    {
        var pipeline = request.PipelineName;
        if (!PrCommentPipelines.Allowed.Contains(pipeline))
        {
            logger.LogInformation("Ignoring PR comment from {Author} on {Pr}: pipeline={Pipeline} is not allowed",
                command.Author.AuthorLogin, command.PrLabel, pipeline);
            return WebhookResult.NotHandled();
        }

        logger.LogInformation("PR comment command from {Author} on {Pr}: pipeline={Pipeline}",
            command.Author.AuthorLogin, command.PrLabel, pipeline);
        var ticketSegment = request.TicketId is not null ? $" #{request.TicketId.Value}" : "";
        return new WebhookResult(true, $"{pipeline}{ticketSegment} {command.PrReference}", pipeline);
    }
}
