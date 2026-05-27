using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Verify-phase orchestrator. Delegates verifier dispatch + observation
/// aggregation to <see cref="IVerifyRoundCoordinator"/>; retains the two-round
/// policy: blocking observations in round 1 trigger re-implementation via
/// InsertNext = [AgenticExecute, RunVerifyPhase]; blocking observations in
/// round 2 escalate by returning Fail with combined deduped notes.
/// </summary>
public sealed class VerifyRoundHandler(
    IVerifyRoundCoordinator coordinator,
    ILogger<VerifyRoundHandler> logger) : ICommandHandler<RunVerifyPhaseContext>
{
    public async Task<CommandResult> ExecuteAsync(
        RunVerifyPhaseContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var roundCount = AdvanceRoundCount(pipeline);

        if (!TryReadInputs(pipeline, out var planJson, out var diffJson))
            return CommandResult.Ok("Verify phase skipped — no Plan/Diff or AvailableRoles in context");

        var round = await coordinator.RunRoundAsync(
            planJson, diffJson, context.AgentConfig, pipeline, cancellationToken);
        if (round.VerifierCount == 0)
            return CommandResult.Ok("Verify phase: no active verifiers");

        AppendObservations(pipeline, round.Observations);
        var blocking = round.Observations.Count(o => o.Blocking);
        logger.LogInformation(
            "Verify phase round {Round}: {Verifiers} verifier(s), {Total} observation(s), {Blocking} blocking",
            roundCount, round.VerifierCount, round.Observations.Count, blocking);

        if (blocking == 0)
            return CommandResult.Ok($"Verify round {roundCount}: {round.Observations.Count} observations, none blocking");

        var notes = VerifyNotesFormatter.Format(roundCount, round.Observations);
        return roundCount >= 2
            ? Escalate(pipeline, notes)
            : ReLoop(pipeline, notes, round.Observations.Count, blocking);
    }

    private static int AdvanceRoundCount(PipelineContext pipeline)
    {
        var current = pipeline.TryGet<int>(ContextKeys.VerifyRoundCount, out var c) ? c : 0;
        var next = current + 1;
        pipeline.Set(ContextKeys.VerifyRoundCount, next);
        return next;
    }

    private static bool TryReadInputs(PipelineContext pipeline, out string planJson, out string diffJson)
    {
        planJson = pipeline.TryGet<string>(ContextKeys.PlanJson, out var p) ? p ?? string.Empty : string.Empty;
        diffJson = pipeline.TryGet<string>(ContextKeys.DiffJson, out var d) ? d ?? string.Empty : string.Empty;
        var hasRoles = pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, out var r) && r is { Count: > 0 };
        return hasRoles
            && (!string.IsNullOrWhiteSpace(planJson) || !string.IsNullOrWhiteSpace(diffJson));
    }

    private static void AppendObservations(PipelineContext pipeline, IReadOnlyList<SkillObservation> observations)
    {
        if (!pipeline.TryGet<List<SkillObservation>>(ContextKeys.VerifyObservations, out var existing) || existing is null)
            existing = [];
        existing.AddRange(observations);
        pipeline.Set(ContextKeys.VerifyObservations, existing);
    }

    private CommandResult ReLoop(PipelineContext pipeline, string notes, int total, int blocking)
    {
        pipeline.Set(ContextKeys.VerifyNotes, notes);
        logger.LogInformation("Verify round 1: {Blocking}/{Total} blocking — re-implementing", blocking, total);
        return CommandResult.OkAndContinueWith(
            $"Verify round 1: {blocking} blocking observation(s), re-implementing",
            PipelineCommand.Simple(CommandNames.AgenticExecute),
            PipelineCommand.Simple(CommandNames.RunVerifyPhase));
    }

    private CommandResult Escalate(PipelineContext pipeline, string notes)
    {
        var combined = BuildCombinedDedupedNotes(pipeline) ?? notes;
        pipeline.Set(ContextKeys.VerifyNotes, combined);
        logger.LogWarning("Verify round 2: blocking observations after re-implementation; escalating");
        return CommandResult.Fail(
            $"Verify-phase escalation: second blocking observation; pipeline ends.\n\n{combined}");
    }

    private static string? BuildCombinedDedupedNotes(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<List<SkillObservation>>(ContextKeys.VerifyObservations, out var all)
            || all is null || all.Count == 0)
            return null;
        var blocking = VerifyNotesFormatter.Dedup(all).Where(o => o.Blocking).ToList();
        return blocking.Count == 0 ? null : VerifyNotesFormatter.Format(round: 2, blocking);
    }
}
