using System.ComponentModel;
using System.Globalization;
using AgentSmith.Application.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0315d: the ask_human surface for TICKET-triggered runs (phase-execution).
/// There is no live dialogue transport on an ephemeral run container, so a
/// question cannot block for an answer — instead the question is
/// captured here and the tool tells the master to stop; after the loop the
/// MasterOpenQuestions step posts it as a p0318 open-questions ticket comment
/// and parks the ticket in needs_clarification_status. The operator's
/// answering comment plus the status move re-trigger a fresh run whose
/// prompt carries the answers (PlanAnswers).
/// <para>2026-11-23-31dch: each question names who can answer it, and one question per answerer is open at a time — a
/// plan gap and an environment fault are two questions for two people, and the run parks once with both.</para>
/// </summary>
public sealed class TicketClarificationToolHost : IToolHost
{
    private readonly List<PlanOpenQuestion> _asked = [];

    /// <summary>2026-11-22-96c8a: true on a spec derivation, whose question is handed back and ends the run.</summary>
    public bool Derivation { get; init; }

    /// <summary>The questions the master asked this run, one per answerer, in the order asked. Each id is its ordinal —
    /// the "Q1:", "Q2:" of the ticket comment and the anchor the reply parser matches; an ask's answerable identity is
    /// minted per ask by MasterQuestionCheckpoint (2026-08-25-a508).</summary>
    public IReadOnlyList<PlanOpenQuestion> Asked => _asked;

    /// <summary>The master asked at least one question; the run parks.</summary>
    public bool HasAsked => _asked.Count > 0;

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(AskHuman, name: "ask_human")];
    }

    [Description("Posts one question for the person who can answer it. The run then stops and continues in a new pass with the answer in its input. One question per answerer is open at a time: a second to the same answerer is refused, one to another answerer is posted too. Ask what blocks the work and only a person can answer — a choice someone may overrule later is decide_by_default's.")]
    public string AskHuman(
        [Description("Question text to display to the human.")] string question,
        [Description("Required. Who can answer: 'target' — what the code this ticket changes should do, answered on the ticket; 'plan' — the plan across tickets (a contract between tickets, their order or scope), answered in the series conversation; 'environment' — the environment the run works in (an unreachable repository, a missing credential, image or service), reported to the admin.")] string? answerer = null,
        [Description("Optional context block shown alongside the question.")] string? context = null,
        [Description("Optional list of choices. Each entry is {label, description?}.")] IReadOnlyList<HumanToolHost.AskHumanChoice>? choices = null,
        [Description("Optional 0-based index into choices identifying the recommended option.")] int? recommended_index = null)
    {
        if (QuestionAnswerers.Parse(answerer) is not { } who) return QuestionAnswerers.Refusal(answerer);
        // 2026-11-22-96c8a: a derivation hands back one question and ends; a second would never reach anyone.
        if (Derivation && HasAsked) return "A question is already handed back and this derivation ends. Stop now — end your reply.";
        if (_asked.Any(q => q.Answerer == who))
            return $"A question to '{who}' is already posted and this run is pausing for its answer. "
                + "Stop now — end your reply.";
        _asked.Add(Question(question, who, context, choices, recommended_index));
        return Derivation
            // 2026-11-22-96c8a: a spec derivation ends on the question; the answer starts a new run.
            ? "Your question will be posted to the ticket and this derivation ends; it resumes with the answer as a "
              + "new run. STOP now: make no further changes, and end your reply with a one-line note that you asked."
            : "This run cannot receive a live answer. Your question will be posted and the run pauses until it is "
              + "answered; it then continues with the answer in your input. If another answerer must settle something "
              + "else, ask that now with its own answerer. Then STOP: make no further changes, "
              + "and end your reply with a one-line note that you are waiting for the answer.";
    }

    private PlanOpenQuestion Question(
        string question, string answerer, string? context,
        IReadOnlyList<HumanToolHost.AskHumanChoice>? choices, int? recommended)
    {
        var text = string.IsNullOrWhiteSpace(context) ? question : $"{question}\n\n{context}";
        var valid = recommended is { } at && choices is not null && at >= 0 && at < choices.Count ? recommended : null;
        // 2026-11-09-0620f: the recommended option reaches the ticket instead of being dropped.
        if (valid is { } i) text += $"\n\nRecommended: {choices![i].label}";
        var ordinal = (_asked.Count + 1).ToString(CultureInfo.InvariantCulture);
        return new PlanOpenQuestion(ordinal, text, choices?.Select(c => c.label).ToList() ?? [])
        {
            RecommendedIndex = valid, Answerer = answerer,
        };
    }
}
