using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Human-escalation host: exposes the AskHuman tool in every phase when a
/// dialogue transport + jobId are configured. Whether the LLM may interrupt
/// the operator is an operator-policy question (handled via IPipelineToolPolicy
/// or DI configuration), never a phase question.
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

    [Description("Asks the human operator for guidance via the dialogue transport.")]
    public async Task<string> AskHuman(
        [Description("Question text to display to the human.")] string question,
        [Description("Optional context block shown alongside the question.")] string? context = null,
        [Description("Optional list of choices for multiple-choice answers.")] IReadOnlyList<string>? choices = null,
        CancellationToken ct = default)
    {
        if (_dialogueTransport is null || _jobId is null)
            return "Error: Dialogue transport not configured.";
        var qid = Guid.NewGuid().ToString("N");
        var qType = choices is { Count: > 0 } ? QuestionType.Choice : QuestionType.FreeText;
        var dq = new DialogQuestion(qid, qType, question, context, choices, "", TimeSpan.FromMinutes(5));
        await _dialogueTransport.PublishQuestionAsync(_jobId, dq, ct);
        var answer = await _dialogueTransport.WaitForAnswerAsync(_jobId, qid, TimeSpan.FromMinutes(5), ct);
        return answer is null ? "Answer (timeout): " : $"Answer: {answer.Answer}";
    }
}
