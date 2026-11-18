using System.ComponentModel;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207d: the verify master's record of one criterion — holds or not, and what showed it. A
/// criterion never recorded counts as not shown to hold.</summary>
public sealed class SeriesVerdictTool(IReadOnlyList<SeriesCriterion> criteria)
{
    private readonly Dictionary<int, SeriesCriterionResult> _results = [];

    public AIFunction GetTool() => AIFunctionFactory.Create(RecordCriterion, "record_criterion");

    [Description("Records whether one numbered criterion holds on the default branch, and the evidence: the command and its outcome, the file and line, the test.")]
    public string RecordCriterion(
        [Description("The criterion's number from the list.")] int number,
        [Description("True when it holds.")] bool holds,
        [Description("What showed it — cite commands, files, tests.")] string evidence)
    {
        if (number < 1 || number > criteria.Count) return $"There are criteria 1 to {criteria.Count}; {number} is none of them.";
        lock (_results) _results[number] = new SeriesCriterionResult(criteria[number - 1].Cited, holds, evidence);
        return $"Recorded criterion {number}.";
    }

    public IReadOnlyList<SeriesCriterionResult> Results()
    {
        lock (_results)
            return [.. criteria.Select((c, i) => _results.GetValueOrDefault(i + 1)
                ?? new SeriesCriterionResult(c.Cited, false, "not checked: the verify run recorded nothing for it"))];
    }
}
