using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.ChatRuns;

/// <summary>
/// Carries an answer given in a chat thread to the run bound to that thread, through the
/// durable dialogue transport — the answer inbox first, so it reaches a run parked for days and
/// a run resumed after a restart as surely as one still waiting live. The binding, not the
/// conversation state, says which run a thread's answer belongs to, so an answer given after
/// that state expired still arrives.
/// </summary>
public sealed class ChatRunAnswerRouter(
    IChatRunBindingStore bindings,
    IDialogueTransport dialogue,
    IDialogueAnswerInbox inbox,
    TimeProvider timeProvider,
    ILogger<ChatRunAnswerRouter> logger)
{
    /// <summary>A button or card answer to <paramref name="questionId"/>. False when the thread
    /// has no open run asking that question.</summary>
    public async Task<bool> TryAnswerAsync(
        ChatThread thread, string questionId, string answer, string? comment, CancellationToken ct)
    {
        var binding = await BoundAsync(thread, ct);
        if (binding is null || binding.QuestionId != questionId) return false;
        await DeliverAsync(binding, answer, comment, thread.RequestedBy, ct);
        return true;
    }

    /// <summary>A message written in the thread, taken as the answer when the run's open question
    /// asks for free text and has no answer yet.</summary>
    public async Task<bool> TryAnswerFromMessageAsync(ChatThread thread, string text, CancellationToken ct)
    {
        var binding = await BoundAsync(thread, ct);
        if (binding?.QuestionId is not { } questionId || QuestionOf(binding)?.Type != QuestionType.FreeText)
            return false;
        if (await inbox.GetAsync(binding.RunId, questionId, ct) is not null) return false;
        await DeliverAsync(binding, text, null, thread.RequestedBy, ct);
        return true;
    }

    private Task<ChatRunBindingFact?> BoundAsync(ChatThread thread, CancellationToken ct) =>
        bindings.FindOpenInThreadAsync(thread.Platform, thread.ChannelId, thread.ThreadId, ct);

    private async Task DeliverAsync(
        ChatRunBindingFact binding, string answer, string? comment, string answeredBy, CancellationToken ct)
    {
        var text = ChoiceLabel(QuestionOf(binding), answer);
        await dialogue.PublishAnswerAsync(binding.RunId,
            new DialogAnswer(binding.QuestionId!, text, comment, timeProvider.GetUtcNow(), answeredBy), ct);
        logger.LogInformation(
            "Chat answer to '{QuestionId}' delivered to run {RunId}", binding.QuestionId, binding.RunId);
    }

    // A choice button answers by index; the run asked with labels, so it is given the label.
    private static string ChoiceLabel(DialogQuestion? question, string answer) =>
        question is { Type: QuestionType.Choice, Choices: { } choices }
        && int.TryParse(answer, out var index) && index >= 0 && index < choices.Count
            ? choices[index].Label
            : answer;

    private static DialogQuestion? QuestionOf(ChatRunBindingFact binding) =>
        binding.QuestionJson is { Length: > 0 } json ? JsonSerializer.Deserialize<DialogQuestion>(json) : null;
}
