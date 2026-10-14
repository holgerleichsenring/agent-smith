using System.ComponentModel;
using AgentSmith.Application.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-13-8762o: decide_by_default — the master takes a choice a person might overrule but need not
/// make first, and goes on in the same call. The choice becomes a pending row a person can ratify or
/// overrule later; nothing waits on it.
/// </summary>
public sealed class DefaultDecisionToolHost(DefaultDecisionRecorder recorder) : IToolHost
{
    public const string Name = "decide_by_default";
    public const int MaxQuestionChars = 500;
    public const int MaxChosenChars = 500;
    public const int MaxWhyChars = 1000;
    public const int MaxAlternatives = 8;
    public const int MaxAlternativeChars = 300;

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(DecideByDefault, name: Name)];
    }

    [Description("Takes a choice a person might overrule but need not make before work continues, and returns at " +
                 "once: the choice is recorded for a person to ratify or overrule later, and you go on with it. " +
                 "holds_series true holds only the next automatic attempt of the series until a person looks; it " +
                 "never stops this run. A choice that needs a person BEFORE work continues stays ask_human. A " +
                 "default is recorded here only: do not also log it with log_decision or write it to decisions.md. " +
                 "Question and chosen at most 500 characters, why at most 1000, one to eight alternatives of at " +
                 "most 300 each.")]
    public async Task<string> DecideByDefault(
        [Description("The question the choice answers.")] string question,
        [Description("What you chose.")] string chosen,
        [Description("The alternatives you did not choose, one to eight.")] string[] alternatives,
        [Description("Why this choice, in a sentence or two.")] string why,
        [Description("True holds the next automatic attempt of the series until a person looks.")] bool holds_series = false,
        CancellationToken ct = default)
    {
        var options = (alternatives ?? []).Select(a => a?.Trim() ?? string.Empty).ToList();
        var refusals = Refusals(question?.Trim(), chosen?.Trim(), options, why?.Trim()).ToList();
        if (refusals.Count > 0)
            return $"Error: decide_by_default refused, nothing was recorded: {string.Join("; ", refusals)}.";

        var recorded = await recorder.RecordAsync(question!.Trim(), chosen!.Trim(), options, why!.Trim(), holds_series, ct);
        return recorded
            ? $"Taken by default, recorded for ratification: {chosen.Trim()}. Go on with it."
            : $"NOT recorded — the run's event stream is unavailable, so no person will see this default: " +
              $"{chosen.Trim()}. Ask with ask_human if a person must know.";
    }

    private static IEnumerable<string> Refusals(string? question, string? chosen, List<string> alternatives, string? why)
    {
        foreach (var refusal in Text("question", question, MaxQuestionChars)) yield return refusal;
        foreach (var refusal in Text("chosen", chosen, MaxChosenChars)) yield return refusal;
        foreach (var refusal in Text("why", why, MaxWhyChars)) yield return refusal;
        if (alternatives.Count is 0 or > MaxAlternatives)
            yield return $"'alternatives' has {alternatives.Count} entries, one to {MaxAlternatives} are needed";
        for (var i = 0; i < alternatives.Count; i++)
            foreach (var refusal in Text($"alternatives entry {i + 1}", alternatives[i], MaxAlternativeChars))
                yield return refusal;
    }

    private static IEnumerable<string> Text(string name, string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) yield return $"'{name}' is required";
        else if (value.Length > max) yield return $"'{name}' is {value.Length} characters, at most {max}";
    }
}
