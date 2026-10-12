using AgentSmith.Application.Services;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Decides what a PR/MR comment starts, the same way on every code host: our own marked notes
/// never command; the structural command match first (an ordinary comment costs nothing), then
/// the author's write access on the repository (an untrusted command costs no model call).
/// On a ticket branch of the same repository the command IS a rework act: the ticket is nudged and
/// the rework reads every open review thread. Elsewhere the model resolves the command to a
/// pipeline <see cref="PrCommentPipelines.Allowed"/> names; a bare command there means nothing.
/// </summary>
public sealed class PrCommentCommandAdmission(
    CommentIntentParser commentIntentParser,
    ServerContext serverContext,
    IPrCommandLaunch launch,
    IPrTicketOwner owners,
    IReworkNudges nudges,
    ILogger<PrCommentCommandAdmission> logger)
{
    public async Task<WebhookResult> AdmitAsync(
        PrCommentCommand command, IPrCommentAuthorTrust trust, CancellationToken cancellationToken,
        IReadOnlySet<string>? allowedProjects = null)
    {
        if (command.Body.StartsWith(OwnPrNoteMarker.Prefix, StringComparison.Ordinal))
            return WebhookResult.NotHandled("our own note");
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
        if (TicketOf(command) is { } ticketId)
            return await NudgeAsync(command, ticketId, allowedProjects, cancellationToken);
        if (string.IsNullOrEmpty(match.Tail))
            return WebhookResult.NotHandled("a bare command off a ticket branch asks for nothing");

        var request = await commentIntentParser.ResolveAsync(
            match.Tail, serverContext.ConfigPath, cancellationToken);
        return await RouteAsync(command, request, cancellationToken, allowedProjects);
    }

    private static string? TicketOf(PrCommentCommand command) =>
        command.SameRepository && !string.IsNullOrEmpty(command.PrUrl) ? TicketBranchNamer.TicketOf(command.HeadRef) : null;

    private async Task<WebhookResult> NudgeAsync(
        PrCommentCommand command, string ticketId, IReadOnlySet<string>? allowedProjects, CancellationToken ct)
    {
        if (await owners.FindAsync(command.Author.RepositoryUrl, ticketId, allowedProjects, ct) is not { } owner)
            return WebhookResult.NotHandled($"no single configured project has a run on ticket {ticketId}");
        logger.LogInformation("PR comment command from {Author} on {Pr}: rework of ticket {Ticket}",
            command.Author.AuthorLogin, command.PrLabel, ticketId);
        await nudges.EnqueueAsync(new ReworkNudgeRequest(owner.ProjectName, ticketId, ReworkNudgeOrigin.PullRequest,
            command.PrUrl, ReworkChannel.PullRequest), ct);
        return WebhookResult.HandledNoRoute();
    }

    private WebhookResult Refuse(PrCommentCommand command)
    {
        logger.LogInformation(
            "Ignoring PR comment command from {Author} on {Pr}: the author may not write to the repository",
            command.Author.AuthorLogin, command.PrLabel);
        return WebhookResult.NotHandled("comment author may not write to the repository");
    }

    // 2026-10-08-e8b9e: no trigger input any more — the free-text path parsed the command a second
    // time and attached its run to whatever lease the ticket had. The launch claims, and answers.
    private async Task<WebhookResult> RouteAsync(
        PrCommentCommand command, PipelineRequest request, CancellationToken ct, IReadOnlySet<string>? allowedProjects)
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
        return await launch.LaunchAsync(command, request, ct, allowedProjects);
    }
}
