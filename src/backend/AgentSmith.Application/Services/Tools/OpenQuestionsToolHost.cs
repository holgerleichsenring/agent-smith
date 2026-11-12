using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-10-d854a: state_open_questions — the design master lays the questions only the operator can answer onto
/// this turn's draft, each with its proposal and the spec ids it touches. The list lands on the reply slot and rides
/// the proposal; a later call replaces it; an entry with an empty question or proposal is refused with the rule.
/// </summary>
public sealed class OpenQuestionsToolHost(SpecDialogReplySlot slot) : IToolHost
{
    public const string Name = "state_open_questions";

    internal const string Rule = "Each entry needs a question and your proposal.";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(StateOpenQuestions, name: Name)];
    }

    [Description(
        "Lays the questions only the operator can answer onto this turn's draft, for its card: each entry is the "
        + "question, your proposal, and the ids of the specs it touches. The operator takes your proposal or answers "
        + "differently; their answers come back together as one message, and a question still open when they approve "
        + "is filed as decided on your proposal. The questions show with the draft on "
        + "every surface, so your reply need not repeat them. A later call in this turn replaces the list.")]
    private string StateOpenQuestions(
        [Description("The questions, each {question, proposal, specIds}.")] OpenQuestion[] questions)
    {
        var entries = questions ?? [];
        for (var i = 0; i < entries.Length; i++)
        {
            var refusal = Refusal(entries[i]);
            if (refusal is not null) return $"Not stored: entry {i + 1} has an empty {refusal}. {Rule}";
        }
        slot.Questions = [.. entries.Select(Normalized)];
        return $"Stored {entries.Length} question(s)."; // 2026-11-12-cd4cf: the one channel; nothing to repeat
    }

    private static string? Refusal(OpenQuestion? entry)
    {
        if (string.IsNullOrWhiteSpace(entry?.Question)) return "question";
        return string.IsNullOrWhiteSpace(entry.Proposal) ? "proposal" : null;
    }

    private static OpenQuestion Normalized(OpenQuestion entry) => new(
        entry.Question.Trim(), entry.Proposal.Trim(),
        [.. (entry.SpecIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal)]);
}
