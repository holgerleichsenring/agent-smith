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

    [Description("Records whether one numbered criterion holds on the checked-out branch, and the evidence: the command and its outcome, the file and line, the test. "
        + "When this environment keeps it from being checked at all — a secret or service it lacks, a feed still unreachable after the staged credentials — give notCheckable instead: it is neither held nor failed.")]
    public string RecordCriterion(
        [Description("The criterion's number from the list.")] int number,
        [Description("True when it holds.")] bool holds,
        [Description("What showed it — cite commands, files, tests.")] string evidence,
        [Description("Only when this environment cannot check it: what is missing here and what would make it checkable, e.g. 'the disposable database connection string is not set; set ConnectionStrings__Test'. A red build or a failing test is not this: record it as not holding.")] string? notCheckable = null)
    {
        if (number < 1 || number > criteria.Count) return $"There are criteria 1 to {criteria.Count}; {number} is none of them.";
        var why = string.IsNullOrWhiteSpace(notCheckable) ? null : notCheckable.Trim(); // 2026-11-28-d1ffa
        lock (_results) _results[number] = new SeriesCriterionResult(criteria[number - 1].Cited, holds && why is null, evidence) { NotCheckable = why };
        return why is null ? $"Recorded criterion {number}." : $"Recorded criterion {number} as not checkable here.";
    }

    public IReadOnlyList<SeriesCriterionResult> Results()
    {
        lock (_results)
            return [.. criteria.Select((c, i) => _results.GetValueOrDefault(i + 1)
                ?? new SeriesCriterionResult(c.Cited, false, "not checked: the verify run recorded nothing for it"))];
    }
}
