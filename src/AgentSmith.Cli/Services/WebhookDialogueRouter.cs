using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Routes dialogue answers from webhook events to waiting agent jobs
/// via <see cref="IDialogueTransport"/>.
/// </summary>
internal sealed class WebhookDialogueRouter(
    IServiceProvider services,
    ILogger logger)
{
    public async Task RouteAsync(DialogueAnswerData data)
    {
        var lookup = services.GetService<IConversationLookup>();
        var transport = services.GetService<IDialogueTransport>();

        if (lookup is null || transport is null)
        {
            logger.LogWarning("Dialogue answer received but IConversationLookup or IDialogueTransport not registered");
            return;
        }

        var conversation = await lookup.FindByPrAsync(
            data.Platform, data.RepoFullName, data.PrIdentifier, CancellationToken.None);

        if (conversation is null)
        {
            logger.LogWarning("No active job found for PR {Repo}#{Pr}", data.RepoFullName, data.PrIdentifier);
            return;
        }

        if (conversation.PendingQuestionId is null)
        {
            logger.LogWarning("Job {JobId} has no pending question", conversation.JobId);
            return;
        }

        var answer = new DialogAnswer(
            QuestionId: conversation.PendingQuestionId,
            Answer: data.Answer,
            Comment: data.Comment,
            AnsweredAt: DateTimeOffset.UtcNow,
            AnsweredBy: data.AuthorLogin);

        await transport.PublishAnswerAsync(conversation.JobId, answer, CancellationToken.None);

        logger.LogInformation(
            "Published dialogue answer ({Answer}) for job {JobId}, question {QuestionId}",
            data.Answer, conversation.JobId, conversation.PendingQuestionId);
    }
}
