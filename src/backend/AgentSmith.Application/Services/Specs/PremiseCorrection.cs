using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-c47ea: what a false premise does to a spec a person APPROVED. The approval stands on the spec's goal and
/// done criteria, not on every fact it cites, so a premise a look proved false is a corrected fact: the master is told
/// what the spec claimed and what the look showed, the run's decision log records the deviation, and the spec is built.
/// Only a false premise that makes a done criterion impossible to meet or test as written is the operator's question —
/// then every false premise of the spec goes through the existing hand-back, and nothing is recorded as corrected.
/// A set nobody approved (derived) is handed back as before: no person stands behind its facts, and the hand-back is
/// where one first reads them.
/// </summary>
public sealed class PremiseCorrection(
    ISpecProgressRecorder progress, IDecisionLogger decisions, ILogger<PremiseCorrection> logger)
{
    /// <summary>True when the findings are corrections rather than a hand-back.</summary>
    public static bool Applies(PipelineContext pipeline, IReadOnlyList<PremiseFinding> stillFalse)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(stillFalse);
        return stillFalse.Count > 0 && stillFalse.All(f => f.Breaks is null)
            && pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) && set?.Approval is not null;
    }

    /// <summary>Records the corrections and hands them to the master; answers the step's line.</summary>
    public async Task<string> ApplyAsync(
        PipelineContext pipeline, SpecDraft draft, IReadOnlyList<PremiseFinding> corrections, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(corrections);
        var lines = corrections.Select(Line).ToList();
        pipeline.Set<IReadOnlyList<string>>(ContextKeys.PremiseCorrections, lines);
        foreach (var line in lines)
            await decisions.LogAsync(null, DecisionCategory.Implementation,
                $"Spec {draft.SpecId} is approved and is built on a corrected premise: {line}", ct, "premise-check");
        var summary = $"Premise corrected in {draft.SpecId} (approved spec, no done criterion broken): "
            + string.Join(" | ", lines);
        await progress.RecordAsync(pipeline, draft.SpecId, SpecRunState.InProgress, note: summary, cancellationToken: ct);
        logger.LogInformation("{Summary}", summary);
        return summary;
    }

    /// <summary>2026-11-17-c47ea: the approved spec's premises a look proved false, as facts the master builds on.
    /// 2026-11-17-ec48a: a criterion one of them breaks is revised with revise_spec.</summary>
    public static string PromptBlock(PipelineContext pipeline) =>
        !pipeline.TryGet<IReadOnlyList<string>>(ContextKeys.PremiseCorrections, out var corrected) || corrected is not { Count: > 0 }
            ? string.Empty
            : "\n**Corrected premises — the approved spec below claims these, and a look taken before your work showed "
              + "otherwise. Build on what the look showed and keep the spec's goal and done criteria; log the deviation with "
              + "log_decision. If a done criterion cannot be met or tested as written because of it, revise the spec with "
              + "revise_spec — one review of the changed text decides — or ask with ask_human when only the operator can "
              + "say what the criterion should become.**\n"
              + string.Join("\n", corrected.Select(c => "- " + c));

    /// <summary>What the spec claimed, what the look showed, and the evidence the look minted.</summary>
    public static string Line(PremiseFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var evidence = string.IsNullOrWhiteSpace(finding.Evidence) ? string.Empty : $" — evidence {finding.Evidence.Trim()}";
        var looked = string.IsNullOrWhiteSpace(finding.Looked) ? string.Empty : $" (looked at {finding.Looked.Trim()})";
        return $"the spec claims \"{Clean(finding.Premise)}\"; the look showed: {Clean(finding.Why)}{looked}{evidence}";
    }

    private static string Clean(string? text) =>
        (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
}
