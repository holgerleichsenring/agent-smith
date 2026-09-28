using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.ChatRuns;

/// <summary>
/// Posts the question a bound run is asking to its thread, once. The question is read where the
/// run left it: the parked checkpoint (durable — it outlives the hot stream and every restart),
/// else the run's live dialogue stream. A question already answered, or already posted, is not
/// posted again; recording it on the binding is conditional, so one replica posts it.
/// Chat-started runs run in this server, so a run's dialogue identity is its run id.
/// </summary>
public sealed class ChatRunQuestionRelay(
    IRunCheckpointStore checkpoints,
    IDialogueQuestionReader liveQuestions,
    IDialogueAnswerInbox inbox,
    IChatRunBindingStore bindings,
    ChatThreadAdapters adapters,
    ILogger<ChatRunQuestionRelay> logger)
{
    public async Task<bool> RelayAsync(ChatRunBindingFact binding, CancellationToken ct)
    {
        var question = await PendingAsync(binding.RunId, ct);
        if (question is null || question.QuestionId == binding.QuestionId) return false;
        if (await inbox.GetAsync(binding.RunId, question.QuestionId, ct) is not null) return false;
        if (!await bindings.TryRecordQuestionAsync(
                binding.RunId, question.QuestionId, JsonSerializer.Serialize(question), ct))
            return false;

        await PostAsync(ChatThread.Of(binding), question, ct);
        logger.LogInformation(
            "Question '{QuestionId}' of run {RunId} posted to its chat thread", question.QuestionId, binding.RunId);
        return true;
    }

    private async Task<DialogQuestion?> PendingAsync(string runId, CancellationToken ct)
    {
        var checkpoint = await checkpoints.GetByRunIdAsync(runId, ct);
        if (PendingQuestionInfo.FromCheckpoint(checkpoint, logger) is not null)
            return JsonSerializer.Deserialize<DialogQuestion>(checkpoint!.QuestionJson);
        return await liveQuestions.LatestAsync(runId, ct);
    }

    private Task PostAsync(ChatThread thread, DialogQuestion question, CancellationToken ct) =>
        question.Type == QuestionType.Info
            ? adapters.PostAsync(thread, $":information_source: {question.Text}", ct)
            : adapters.PostQuestionAsync(thread, question, ct);
}
