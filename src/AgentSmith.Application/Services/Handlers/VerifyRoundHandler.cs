using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0129a: Verify-phase orchestrator. Runs every active VerifyDiff investigator
/// (role=investigator, investigator_mode=verify_diff, activates_when matches) against
/// the persisted Plan + Diff and aggregates their observations. First blocking
/// observation triggers re-implementation via InsertNext = [AgenticExecute, RunVerifyPhase];
/// the second escalates by returning Fail with combined notes.
/// </summary>
public sealed class VerifyRoundHandler(
    ActivationSkillFilter activationFilter,
    ISkillBodyResolver bodyResolver,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    IDecisionLogger decisionLogger,
    IToolKit toolKit,
    ISkillCallRuntime skillCallRuntime,
    ILogger<VerifyRoundHandler> logger) : ICommandHandler<RunVerifyPhaseContext>
{
    public async Task<CommandResult> ExecuteAsync(
        RunVerifyPhaseContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var roundCount = AdvanceRoundCount(pipeline);

        if (!TryReadInputs(pipeline, out var planJson, out var diffJson, out var roles))
            return CommandResult.Ok("Verify phase skipped — no Plan/Diff or AvailableRoles in context");

        var verifiers = ResolveActiveVerifiers(pipeline, roles);
        if (verifiers.Count == 0)
        {
            logger.LogInformation("Verify phase: no active VerifyDiff investigators; skipping");
            return CommandResult.Ok("Verify phase: no active verifiers");
        }

        var observations = await RunVerifiersAsync(
            verifiers, planJson, diffJson, context.AgentConfig, pipeline, cancellationToken);
        AppendObservations(pipeline, observations);

        var blocking = observations.Count(o => o.Blocking);
        logger.LogInformation(
            "Verify phase round {Round}: {Verifiers} verifier(s), {Total} observation(s), {Blocking} blocking",
            roundCount, verifiers.Count, observations.Count, blocking);

        if (blocking == 0)
            return CommandResult.Ok($"Verify round {roundCount}: {observations.Count} observations, none blocking");

        var notes = VerifyNotesFormatter.Format(roundCount, observations);
        return roundCount >= 2
            ? Escalate(pipeline, notes)
            : ReLoop(pipeline, notes, observations.Count, blocking);
    }

    private static int AdvanceRoundCount(PipelineContext pipeline)
    {
        var current = pipeline.TryGet<int>(ContextKeys.VerifyRoundCount, out var c) ? c : 0;
        var next = current + 1;
        pipeline.Set(ContextKeys.VerifyRoundCount, next);
        return next;
    }

    private static bool TryReadInputs(
        PipelineContext pipeline,
        out string planJson, out string diffJson,
        out IReadOnlyList<RoleSkillDefinition> roles)
    {
        planJson = pipeline.TryGet<string>(ContextKeys.PlanJson, out var p) ? p ?? string.Empty : string.Empty;
        diffJson = pipeline.TryGet<string>(ContextKeys.DiffJson, out var d) ? d ?? string.Empty : string.Empty;
        roles = pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
            ContextKeys.AvailableRoles, out var r) && r is not null ? r : [];
        return roles.Count > 0
            && (!string.IsNullOrWhiteSpace(planJson) || !string.IsNullOrWhiteSpace(diffJson));
    }

    private IReadOnlyList<RoleSkillDefinition> ResolveActiveVerifiers(
        PipelineContext pipeline, IReadOnlyList<RoleSkillDefinition> roles)
    {
        var concepts = conceptsFactory(pipeline);
        var verifiers = roles
            .Where(r => string.Equals(r.Role, "investigator", StringComparison.OrdinalIgnoreCase))
            .Where(r => string.Equals(r.InvestigatorMode, "verify_diff", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return activationFilter.Filter(verifiers, concepts);
    }

    private async Task<List<SkillObservation>> RunVerifiersAsync(
        IReadOnlyList<RoleSkillDefinition> verifiers,
        string planJson, string diffJson, AgentConfig agent,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var combined = new List<SkillObservation>();
        var tools = ResolveVerifyTools(pipeline);
        foreach (var verifier in verifiers)
        {
            var observations = await InvokeVerifierAsync(
                verifier, planJson, diffJson, agent, tools, pipeline, cancellationToken);
            combined.AddRange(observations);
        }
        return combined;
    }

    /// <summary>
    /// p0132c: builds the Verify-phase tool surface for verifiers that want to
    /// invoke RunCommand (build-verifier, test-verifier). When the pipeline has
    /// no active sandbox (smoke tests, no-runtime contexts), returns an empty
    /// tool list so verifiers degrade gracefully to LLM-only static analysis.
    /// Architecture-verifier + scope-verifier ignore the tools regardless —
    /// their SKILL.md bodies don't reference RunCommand.
    /// </summary>
    private IList<AITool> ResolveVerifyTools(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return new List<AITool>();
        var pipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) && pn is not null
            ? pn
            : IToolKit.WildcardPipelineName;
        var hosts = new IToolHost[]
        {
            new FilesystemToolHost(sandbox),
            new LogDecisionToolHost(decisionLogger),
            new HumanToolHost()
        };
        return toolKit.GetToolsFor(
            pipelineName, SkillExecutionPhase.Verify, investigatorMode: "verify_diff", hosts);
    }

    private async Task<List<SkillObservation>> InvokeVerifierAsync(
        RoleSkillDefinition verifier, string planJson, string diffJson,
        AgentConfig agent, IList<AITool> tools,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var body = bodyResolver.ResolveBody(verifier, SkillRole.Analyst);
        var codingPrinciples = pipeline.TryGet<string>(ContextKeys.CodingPrinciples, out var cp) ? cp : null;
        var (system, user) = VerifierPromptBuilder.Build(body, planJson, diffJson, codingPrinciples);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system),
            new(ChatRole.User, user),
        };
        // p0142: per-verifier runtime dispatch — LimitEnforcer + cost
        // attribution per verifier are now load-bearing. ToolSet flows
        // through unchanged (empty when no sandbox → static-only verify).
        var request = new SkillCallRequest
        {
            SkillName = verifier.Name,
            Role = verifier.Role ?? "investigator",
            Phase = SkillExecutionPhase.Verify,
            InvestigatorMode = "verify_diff",
            PromptParts = messages,
            ToolSet = tools.ToList(),
            AgentConfig = agent,
            TaskType = TaskType.Primary,
            PipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null
        };
        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);
        var result = await skillCallRuntime.ExecuteAsync(request, costTracker, cancellationToken);
        if (result.Outcome is not SkillCallOutcome.Ok and not SkillCallOutcome.Incomplete)
        {
            logger.LogWarning(
                "Verifier {Name}: {Outcome} ({Reason}) — no observations from this verifier",
                verifier.Name, result.Outcome, result.FailureReason ?? "no reason");
            return [];
        }
        if (result.Outcome == SkillCallOutcome.Incomplete)
            logger.LogWarning(
                "Verifier {Name} returned Incomplete (limit: {Limit}) — using partial observations",
                verifier.Name, result.Cost.HitLimit ?? "unknown");
        var responseText = result.Output ?? string.Empty;
        var parsed = ObservationParser.ParseWithoutIds(responseText, verifier.Name, logger);
        return ApplyConfidenceThreshold(parsed, verifier.Name);
    }

    private List<SkillObservation> ApplyConfidenceThreshold(
        List<SkillObservation> parsed, string verifierName)
    {
        var result = new List<SkillObservation>(parsed.Count);
        foreach (var obs in parsed)
        {
            if (obs.Blocking && obs.Confidence < 70)
            {
                logger.LogInformation(
                    "Verify {Verifier}: blocking observation '{Concern}' downgraded (confidence {Confidence} < 70)",
                    verifierName, obs.Concern, obs.Confidence);
                result.Add(obs with { Blocking = false });
            }
            else result.Add(obs);
        }
        return result;
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
        logger.LogInformation(
            "Verify round 1: {Blocking}/{Total} blocking — re-running implementation with notes",
            blocking, total);
        return CommandResult.OkAndContinueWith(
            $"Verify round 1: {blocking} blocking observation(s), re-implementing",
            PipelineCommand.Simple(CommandNames.AgenticExecute),
            PipelineCommand.Simple(CommandNames.RunVerifyPhase));
    }

    private CommandResult Escalate(PipelineContext pipeline, string notes)
    {
        // p0129c: dedup across rounds. VerifyObservations holds both round-1 and round-2
        // observations (AppendObservations is cumulative); collapsing duplicates by
        // (file, concern, description-prefix-100) keeps the writeback focused on
        // unique unfixed concerns rather than echoing the same finding twice.
        var combinedNotes = BuildCombinedDedupedNotes(pipeline) ?? notes;
        pipeline.Set(ContextKeys.VerifyNotes, combinedNotes);
        logger.LogWarning(
            "Verify round 2: blocking observations after re-implementation; escalating to ticket");
        return CommandResult.Fail(
            $"Verify-phase escalation: second blocking observation; pipeline ends.\n\n{combinedNotes}");
    }

    private static string? BuildCombinedDedupedNotes(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<List<SkillObservation>>(ContextKeys.VerifyObservations, out var all)
            || all is null || all.Count == 0)
            return null;
        var deduped = VerifyNotesFormatter.Dedup(all);
        var blocking = deduped.Where(o => o.Blocking).ToList();
        return blocking.Count == 0
            ? null
            : VerifyNotesFormatter.Format(round: 2, blocking);
    }
}
