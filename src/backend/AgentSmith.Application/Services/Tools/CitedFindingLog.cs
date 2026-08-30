using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-03e1: where the findings that cite an entry of the standard accumulate while
/// the loop runs.
/// <para>
/// It lives ON the pipeline rather than in the tool host, because the master and every
/// sub-agent it fans out to get their own host over the same run and all contribute to one
/// account. Restating a finding at the same place replaces the earlier one, so a worker
/// that corrects itself does not leave two rows behind.
/// </para>
/// <para>
/// The per-group cap is the one bound that survived the inversion, and it never costs a
/// finding: beyond it the group is reported NOT ATTEMPTED and the recorder sends the
/// finding down the ordinary observation path, which delivers it unchanged.
/// </para>
/// </summary>
public sealed class CitedFindingLog
{
    /// <summary>How many entry groups one run accounts for. Beyond it a group is reported
    /// NOT ATTEMPTED, which is a budget fact and not a verdict.</summary>
    public const int MaxEntryGroups = 5;

    private readonly object _gate = new();
    private readonly List<CitedFinding> _findings = [];
    private readonly List<string> _groups = [];

    public static CitedFindingLog GetOrCreate(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<CitedFindingLog>(ContextKeys.RequirementCitations, out var existing)
            && existing is not null)
            return existing;
        var log = new CitedFindingLog();
        pipeline.Set(ContextKeys.RequirementCitations, log);
        return log;
    }

    /// <summary>What this run cited, or nothing when no entry was ever cited.</summary>
    public static IReadOnlyList<CitedFinding> In(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<CitedFindingLog>(ContextKeys.RequirementCitations, out var log)
            && log is not null ? log.Findings : [];
    }

    public IReadOnlyList<CitedFinding> Findings
    {
        get { lock (_gate) return [.. _findings]; }
    }

    /// <summary>Records the finding, or refuses it when its group lies beyond the cap.</summary>
    public bool Record(CitedFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        lock (_gate)
        {
            if (!Admits(finding.Group)) return false;
            _findings.RemoveAll(existing => Same(existing, finding));
            _findings.Add(finding);
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

    private static bool Same(CitedFinding a, CitedFinding b) =>
        a.Station == b.Station
        && a.StartLine == b.StartLine
        && string.Equals(a.RequirementId, b.RequirementId, StringComparison.Ordinal)
        && string.Equals(a.File, b.File, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
}
