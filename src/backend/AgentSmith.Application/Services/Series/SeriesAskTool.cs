using System.ComponentModel;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207c: the series master's ask_human — one question in the conversation at a time; with a
/// recommendation the series takes it after dialogue.question_default_minutes, without one it waits for the operator.
/// The series goes on meanwhile; the answer reaches the master in a later beat.
/// <para>2026-11-25-96aea: a merge question (mergeOf) always offers "Merge it" and "Look at the pull request first" as buttons
/// with the pull requests' links and is never taken by default — the person decides every merge in the dashboard. A series
/// without a conversation asks nothing.</para></summary>
public sealed class SeriesAskTool(SeriesBeat beat, ISeriesRunStore store, int defaultMinutes)
{
    public const string Name = "ask_human";
    public const string MergeIt = "Merge it";
    public const string LookFirst = "Look at the pull request first";

    public AIFunction GetTool() => AIFunctionFactory.Create(AskHumanAsync, Name);

    [Description("Asks the operator, in the conversation, one decision only they can make. Recommend a choice where you can: it is taken by default at the deadline if nobody answers. With mergeOf (a ticket or 'final') it is a merge question: the choices 'Merge it' and 'Look at the pull request first' and the pull requests' links are added, and it waits for the person. The series goes on meanwhile and the answer reaches you in a later beat.")]
    public async Task<string> AskHumanAsync(
        [Description("The question.")] string question,
        [Description("The choices, each {label, description?}.")] IReadOnlyList<HumanToolHost.AskHumanChoice>? choices = null,
        [Description("0-based index of the recommended choice.")] int? recommended_index = null,
        [Description("For a merge question: the ticket's key or id, or 'final'.")] string? mergeOf = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(beat.Record.Conversation))
            return "This series has no conversation to ask in; its ticket's page shows what waits on a person (its merge too).";
        if (beat.Record.Question is { Answer: null } waiting)
            return $"A question already waits for the operator: \"{waiting.Text}\". Its answer comes first.";
        var asked = string.IsNullOrWhiteSpace(mergeOf)
            ? SeriesQuestions.Ask(question, choices?.Select(c => c.label).ToList() ?? [], recommended_index, beat.Now, defaultMinutes)
            : SeriesMergeQuestion.Of(beat, question, mergeOf);
        if (await store.UpdateAsync(beat.Record.Id,
                r => r with { Question = asked, MasterQuestionsAsked = r.MasterQuestionsAsked + (r.Question is { Answer: null } ? 0 : 1) },
                ct) is { } kept) // 2026-11-25-9992k: one replacing a waiting question is not a second ask
            beat.Record = kept;
        beat.Said.Add(SeriesQuestionText.Asked(asked, null));
        return asked.DeadlineAt is { } at
            ? $"Asked. Without an answer the series takes \"{asked.Recommended}\" at {at:u}."
            : "Asked. It waits for the operator's answer.";
    }
}
