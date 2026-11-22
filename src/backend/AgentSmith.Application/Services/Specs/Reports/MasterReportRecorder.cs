using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Reports;

/// <summary>
/// 2026-11-22-96c8a: what a spec's coding master leaves the pipeline once it ended — its report as written, and what
/// one read takes off it. Nothing read is invented: a refused instruction is kept only when its quote appears in the
/// report or a decision line, a follow-up only with a goal, and the build note and memories pass the build-note
/// writer's own checks. Follow-ups add up per spec and refused instructions per run (<see cref="MasterReportFacts"/>).
/// The read is skipped when there is no report or the cost budget is spent; anything that fails — the read, a memory
/// store, the note — leaves the report, names the problem and never fails the step: the stages decide delivery.
/// </summary>
public sealed class MasterReportRecorder(MasterReportReader reader, ILogger<MasterReportRecorder> logger)
{
    public const string BudgetSpent = "the cost budget is spent";

    public async Task<MasterVerification> RecordAsync(
        PipelineContext pipeline, AgentConfig agent, string? report, IReadOnlyList<PlanDecision> decisions,
        SpecHandForward? forward, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (report is null) return new MasterVerification(null);
        try
        {
            var cost = PipelineCostTracker.GetOrCreate(pipeline);
            if (cost.IsBudgetExhausted) return new MasterVerification(report, ReadProblem: BudgetSpent);

            var lines = decisions.Select(d => d.Decision).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            var read = await reader.ReadAsync(report, lines, Handlers.MasterPromptSections.BuildMemoryIndexSection(pipeline), agent, cost, ct);
            if (read.Extract is not { } extract) return new MasterVerification(report, ReadProblem: read.Problem);

            var followUps = FollowUps(extract);
            var ignored = Ignored(extract, report, lines);
            if (pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var spec) && spec is not null)
                MasterReportFacts.AddFollowUps(pipeline, spec.SpecId, followUps);
            MasterReportFacts.AddIgnored(pipeline, ignored);
            if (forward is not null)
                await forward.RecordReadAsync(extract.Renamed, extract.Moved, extract.Persisted, extract.Deviations, extract.ReliedOn, ct);
            return new MasterVerification(report, followUps, ignored);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Recording what the report names failed — the report stands, the stages decide");
            return new MasterVerification(report, ReadProblem: $"recording it failed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private static IReadOnlyList<MasterFollowUp> FollowUps(MasterReportExtract extract) =>
    [
        .. (extract.FollowUps ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Goal))
            .Select(f => new MasterFollowUp(f.Goal.Trim(), f.Why?.Trim() ?? string.Empty)),
    ];

    private IReadOnlyList<IgnoredInstruction> Ignored(MasterReportExtract extract, string report, IReadOnlyList<string> lines)
    {
        var kept = new List<IgnoredInstruction>();
        foreach (var entry in extract.IgnoredInstructions ?? [])
        {
            var quote = entry.Quote?.Trim();
            if (string.IsNullOrEmpty(quote)) continue;
            if (report.Contains(quote, StringComparison.Ordinal) || lines.Any(l => l.Contains(quote, StringComparison.Ordinal)))
                kept.Add(new IgnoredInstruction(quote, entry.Reason?.Trim() ?? string.Empty));
            else
                logger.LogWarning("Dropped an ignored instruction the report's read named but neither the report nor a decision line quotes: \"{Quote}\"", quote);
        }
        return kept;
    }
}
