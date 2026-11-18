using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-18-d6d2c: the record a verification reads beside the diff — the spec as ratified (its steps with its
/// criteria) and, on the verification after a repair, the previous verification's findings the repair was handed and
/// the files the repair changed. Never the agent's account of its work: what the repair says it did is a claim, the
/// files it changed are a fact.
/// </summary>
public sealed record AccountRecord(
    string? SpecYaml, IReadOnlyList<string> PreviousFindings, IReadOnlyList<string> RepairChanged)
{
    public static readonly AccountRecord None = new(null, [], []);

    /// <summary>True on the verification after an accounting repair — the one that carries findings over.</summary>
    public bool IsAfterRepair => PreviousFindings.Count > 0;

    /// <summary>The record for this spec. A stage repair hands back a red build, not findings, so it carries none.</summary>
    public static AccountRecord For(PipelineContext pipeline, IReadOnlyList<string> repairChanged)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(repairChanged);
        var yaml = pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var draft) ? draft?.Yaml : null;
        var previous = pipeline.TryGet<List<string>>(ContextKeys.OutstandingCriteria, out var outstanding)
            && outstanding is { Count: > 0 }
            && !outstanding.Any(o => o.StartsWith(StageRepair.ItemPrefix, StringComparison.Ordinal))
                ? (IReadOnlyList<string>)outstanding
                : [];
        return new AccountRecord(yaml, previous, previous.Count > 0 ? repairChanged : []);
    }
}
