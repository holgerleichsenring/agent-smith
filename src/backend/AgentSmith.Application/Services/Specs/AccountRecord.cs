using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-18-d6d2c: the record a verification reads beside the diff — the spec as ratified (its steps with its
/// criteria) and, on the verification after a repair, the previous verification's findings the repair was handed and
/// the files the repair changed. Never the agent's account of its work: what the repair says it did is a claim, the
/// files it changed are a fact.
/// 2026-11-18-550ab: with no repair list, the findings the ticket's previous failed attempt stored for the spec
/// (<see cref="FromPreviousAttempt"/>), and the files this attempt changed since it took the spec up.
/// </summary>
public sealed record AccountRecord(
    string? SpecYaml, IReadOnlyList<string> PreviousFindings, IReadOnlyList<string> RepairChanged,
    bool FromPreviousAttempt = false)
{
    public static readonly AccountRecord None = new(null, [], []);

    /// <summary>True on a verification that carries findings over — after a repair, or after a failed attempt.</summary>
    public bool IsAfterRepair => PreviousFindings.Count > 0;

    /// <summary>The record for this spec. A stage repair hands back a red build, not findings, so it carries none.</summary>
    public static AccountRecord For(PipelineContext pipeline, IReadOnlyList<string> repairChanged)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(repairChanged);
        var yaml = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft?.Yaml : null;
        var repair = Findings(pipeline, ContextKeys.OutstandingCriteria);
        var previous = repair.Count > 0 ? repair : Findings(pipeline, ContextKeys.PreviousAttemptFindings);
        return new AccountRecord(yaml, previous, previous.Count > 0 ? repairChanged : [],
            FromPreviousAttempt: repair.Count == 0 && previous.Count > 0);
    }

    private static IReadOnlyList<string> Findings(PipelineContext pipeline, string key) =>
        pipeline.TryGet<List<string>>(key, out var findings) && findings is { Count: > 0 }
        && !findings.Any(o => o.StartsWith(StageRepair.ItemPrefix, StringComparison.Ordinal))
            ? (IReadOnlyList<string>)findings
            : [];
}
