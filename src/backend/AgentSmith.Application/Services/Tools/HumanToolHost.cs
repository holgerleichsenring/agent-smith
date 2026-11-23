using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Human-escalation host: exposes ask_human in every phase when a dialogue
/// transport + jobId are configured. Choices is a flat array of {label,
/// description?} entries; recommendation is carried by a separate top-level
/// recommended_index so the per-choice schema stays two-field flat for
/// OpenAI strict mode and small Ollama tool calling.
/// </summary>
public sealed class HumanToolHost : IToolHost
{
    private readonly IDialogueTransport? _dialogueTransport;
    private readonly string? _jobId;

    public HumanToolHost(IDialogueTransport? dialogueTransport = null, string? jobId = null)
    {
        _dialogueTransport = dialogueTransport;
        _jobId = jobId;
    }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(AskHuman, name: "ask_human")];
    }

    [Description("Asks the human operator for guidance via the dialogue transport. choices is an optional flat list of [{label, description?}]; recommended_index points to a preferred choice when set.")]
    public async Task<string> AskHuman(
        [Description("Question text to display to the human.")] string question,
        [Description("Optional context block shown alongside the question.")] string? context = null,
        [Description("Optional list of choices. Each entry is {label, description?}.")] IReadOnlyList<AskHumanChoice>? choices = null,
        [Description("Optional 0-based index into choices identifying the recommended option.")] int? recommended_index = null,
        [Description("Optional. Who can answer: 'target' (the default) — what the code being changed should do; 'plan' — the plan across tickets; 'environment' — the environment the run works in, reported to the admin.")] string? answerer = null,
        CancellationToken ct = default)
    {
        // 2026-11-23-31dch: optional here; a value that names no answerer is refused with the values it may name.
        var who = answerer is null ? QuestionAnswerers.Target : QuestionAnswerers.Parse(answerer);
        if (who is null) return QuestionAnswerers.Refusal(answerer);
        if (_dialogueTransport is null || _jobId is null)
            return "Error: Dialogue transport not configured.";
        var qid = Guid.NewGuid().ToString("N");
        var rich = choices?.Select(c => new DialogChoice(c.label, c.description)).ToList();
        var qType = rich is { Count: > 0 } ? QuestionType.Choice : QuestionType.FreeText;
        var dq = new DialogQuestion(qid, qType, question, context, rich, "", TimeSpan.FromMinutes(5), recommended_index)
        {
            Answerers = [who],
        };
        await _dialogueTransport.PublishQuestionAsync(_jobId, dq, ct);
        var answer = await _dialogueTransport.WaitForAnswerAsync(_jobId, qid, TimeSpan.FromMinutes(5), ct);
        return answer is null ? "Answer (timeout): " : $"Answer: {answer.Answer}";
    }

    /// <summary>LLM-facing choice shape: two primitive fields, nothing nested.</summary>
    public sealed record AskHumanChoice(
        [property: Description("Short choice label (what the user sees and selects).")] string label,
        [property: Description("Optional explanation of what this choice means or its implications.")] string? description = null);
}
