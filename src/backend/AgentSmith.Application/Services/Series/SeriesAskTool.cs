using System.ComponentModel;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207c: the series master's ask_human — one question in the conversation at a time; with a
/// recommendation the series takes it after dialogue.question_default_minutes, without one it waits for the operator.
/// The series goes on meanwhile; the answer reaches the master in a later beat.</summary>
public sealed class SeriesAskTool(SeriesBeat beat, ISeriesRunStore store, int defaultMinutes)
{
    public AIFunction GetTool() => AIFunctionFactory.Create(AskHumanAsync, "ask_human");

    [Description("Asks the operator, in the conversation, one decision only they can make. Recommend a choice where you can: it is taken by default at the deadline if nobody answers. The series goes on meanwhile and the answer reaches you in a later beat.")]
    public async Task<string> AskHumanAsync(
        [Description("The question.")] string question,
        [Description("The choices, each {label, description?}.")] IReadOnlyList<HumanToolHost.AskHumanChoice>? choices = null,
        [Description("0-based index of the recommended choice.")] int? recommended_index = null,
        CancellationToken ct = default)
    {
        if (beat.Record.Question is { Answer: null } waiting)
            return $"A question already waits for the operator: \"{waiting.Text}\". Its answer comes first.";
        var asked = SeriesQuestions.Ask(question, choices?.Select(c => c.label).ToList() ?? [], recommended_index, beat.Now, defaultMinutes);
        if (await store.UpdateAsync(beat.Record.Id, r => r with { Question = asked }, ct) is { } kept) beat.Record = kept;
        beat.Said.Add(SeriesQuestionText.Asked(asked, null));
        return asked.DeadlineAt is { } at
            ? $"Asked. Without an answer the series takes \"{asked.Recommended}\" at {at:u}."
            : "Asked. It waits for the operator's answer.";
    }
}
