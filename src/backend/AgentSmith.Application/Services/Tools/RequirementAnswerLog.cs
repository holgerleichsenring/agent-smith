using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-3c12: where the answers to the standard's entries accumulate while the loop
/// runs.
/// <para>
/// It lives ON the pipeline rather than in the tool host, because the fan-out is the
/// feasibility here: one sub-agent per entry group, each with its own host over the same
/// run, all contributing to one account. Re-answering an entry replaces the earlier answer,
/// so a worker that corrects itself does not leave two verdicts behind.
/// </para>
/// </summary>
public sealed class RequirementAnswerLog
{
    /// <summary>How many entry groups one run answers for. Beyond it a group is recorded
    /// NOT ATTEMPTED, which is a budget fact and not a verdict.</summary>
    public const int MaxEntryGroups = 5;

    private readonly object _gate = new();
    private readonly List<RequirementAnswer> _answers = [];
    private readonly List<string> _groups = [];

    public static RequirementAnswerLog GetOrCreate(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<RequirementAnswerLog>(ContextKeys.RequirementAnswers, out var existing)
            && existing is not null)
            return existing;
        var log = new RequirementAnswerLog();
        pipeline.Set(ContextKeys.RequirementAnswers, log);
        return log;
    }

    /// <summary>What this run answered, or nothing when no entry was ever answered.</summary>
    public static IReadOnlyList<RequirementAnswer> In(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<RequirementAnswerLog>(ContextKeys.RequirementAnswers, out var log)
            && log is not null ? log.Answers : [];
    }

    public IReadOnlyList<RequirementAnswer> Answers
    {
        get { lock (_gate) return [.. _answers]; }
    }

    /// <summary>Records the answer, or refuses it when its group lies beyond the cap.</summary>
    public bool Record(RequirementAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        lock (_gate)
        {
            if (!Admits(answer.Group)) return false;
            _answers.RemoveAll(a => Same(a, answer));
            _answers.Add(answer);
            return true;
        }
    }

    private bool Admits(string group)
    {
        if (_groups.Contains(group, StringComparer.OrdinalIgnoreCase)) return true;
        if (_groups.Count >= MaxEntryGroups) return false;
        _groups.Add(group);
        return true;
    }

    private static bool Same(RequirementAnswer a, RequirementAnswer b) =>
        a.Station == b.Station
        && a.Operation == b.Operation
        && string.Equals(a.RequirementId, b.RequirementId, StringComparison.Ordinal)
        && string.Equals(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
}
