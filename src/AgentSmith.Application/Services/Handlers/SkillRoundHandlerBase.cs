using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Orchestrates a single skill round — delegates prompt building, LLM calls,
/// gate handling, and upstream context to injected services. Post-p0142 the
/// chat call itself goes through <see cref="ISkillCallRuntime"/> so the
/// LimitEnforcer + RetryCoordinator + LoopTraceCollector + OutcomeClassifier
/// actually run for every round. Subclasses provide only the domain section.
/// </summary>
public abstract class SkillRoundHandlerBase(
    ISkillPromptBuilder promptBuilder,
    IGateRetryCoordinator gateRetryCoordinator,
    IUpstreamContextBuilder upstreamContextBuilder,
    StructuredOutputInstructionBuilder instructionBuilder,
    IChatClientFactory chatClientFactory,
    ISkillCallRuntime skillCallRuntime)
{
    protected IChatClientFactory ChatClientFactory { get; } = chatClientFactory;
    private readonly ISkillCallRuntime _skillCallRuntime = skillCallRuntime;

    private readonly StructuredOutputInstructionBuilder _instructionBuilder = instructionBuilder;

    protected abstract ILogger Logger { get; }
    protected abstract string BuildDomainSection(PipelineContext pipeline);

    /// <summary>
    /// Splits the domain section into a stable prefix (cached across same-round calls)
    /// and a per-skill suffix. Default returns the legacy single-section as the prefix
    /// with an empty suffix — handlers that benefit from prompt caching override this.
    /// </summary>
    protected virtual (string Stable, string PerSkill) BuildDomainSectionParts(PipelineContext pipeline)
        => (BuildDomainSection(pipeline), string.Empty);

    protected virtual string SkillRoundCommandName => "SkillRoundCommand";

    protected async Task<CommandResult> ExecuteRoundAsync(
        string skillName, int round, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
            return CommandResult.Fail("No available roles in pipeline context");

        var role = roles.FirstOrDefault(r => r.Name == skillName);
        if (role is null)
            return CommandResult.Fail($"Role '{skillName}' not found");

        pipeline.Set(ContextKeys.ActiveSkill, skillName);

        if (IsStructuredRound(role, pipeline))
            return await ExecuteStructuredRoundAsync(
                skillName, role, pipeline, cancellationToken);

        return await ExecuteDiscussionRoundAsync(
            skillName, role, roles, round, pipeline, cancellationToken);
    }

    private async Task<CommandResult> ExecuteDiscussionRoundAsync(
        string skillName, RoleSkillDefinition role,
        IReadOnlyList<RoleSkillDefinition> roles, int round,
        PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        pipeline.TryGet<string>(ContextKeys.DomainRules, out var domainRules);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        var existingTests = ResolveExistingTests(pipeline);
        var assignedRole = ResolveAssignedRole(skillName, pipeline);
        var planArtifact = ResolvePlanArtifact(pipeline);

        pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var discussionLog);
        var discussionForPrompt = (IReadOnlyList<DiscussionEntry>)(discussionLog ?? []);

        var (domainStable, domainVariable) = BuildDomainSectionParts(pipeline);
        var (systemPrompt, userPrefix, userSuffix) = promptBuilder.BuildDiscussionPromptParts(
            role, domainStable, domainVariable, projectContext, domainRules, codeMap,
            discussionForPrompt, round, existingTests, assignedRole, planArtifact);

        var agent = pipeline.Get<AgentConfig>(ContextKeys.AgentConfig);
        var combinedUser = string.IsNullOrEmpty(userSuffix) ? userPrefix : $"{userPrefix}\n\n{userSuffix}";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, combinedUser),
        };
        // p0142: dispatch through ISkillCallRuntime so LimitEnforcer +
        // OutcomeClassifier + LoopTraceCollector actually run. Discussion
        // rounds carry an empty ToolSet — the LLM emits observations only.
        var runtimeRequest = new SkillCallRequest
        {
            SkillName = skillName,
            Role = role.Role ?? "investigator",
            Phase = MapPhase(pipeline),
            PromptParts = messages,
            ToolSet = Array.Empty<AITool>(),
            AgentConfig = agent,
            TaskType = TaskType.Primary,
            PipelineName = ResolvePipelineName(pipeline)
        };
        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);
        var runtimeResult = await _skillCallRuntime.ExecuteAsync(runtimeRequest, costTracker, cancellationToken);
        // p0147b: runtime observations (execution-limit / execution-error)
        // flow into the pipeline even when the round otherwise short-circuits,
        // so silent skill drops surface in the final summary.
        BufferRuntimeObservations(pipeline, skillName, round, runtimeResult);
        if (TranslateDiscussionOutcome(runtimeResult, skillName, role) is { } earlyFail)
            return earlyFail;
        var responseText = runtimeResult.Output ?? string.Empty;

        var rawParsed = ObservationParser.ParseWithoutIds(responseText, skillName, Logger);
        var parsed = ApplyConfidenceThreshold(rawParsed, skillName, Logger);
        StorePlanArtifactIfPlanLead(skillName, assignedRole, pipeline, parsed);
        var renderedText = RenderObservationsAsText(parsed);
        var discussionEntry = new DiscussionEntry(
            skillName, role.DisplayName, role.Emoji, round, renderedText);

        var buffer = new SkillRoundBuffer(skillName, round, parsed, discussionEntry, null);
        DispatchBuffer(pipeline, buffer);

        Logger.LogInformation(
            "{Emoji} {DisplayName} (Round {Round}): {Count} observations",
            role.Emoji, role.DisplayName, round, parsed.Count);

        return DetectBlockingFollowUp(parsed, skillName, role, roles, round, pipeline)
            ?? CommandResult.Ok($"{role.DisplayName} (Round {round}): {parsed.Count} observations");
    }

    private static SkillRole? ResolveAssignedRole(string skillName, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<TriageOutput>(ContextKeys.TriageOutput, out var triage) || triage is null)
            return null;
        if (!pipeline.TryGet<PipelinePhase>(ContextKeys.CurrentPhase, out var phase))
            return null;
        if (!triage.Phases.TryGetValue(phase, out var assignment))
            return null;
        if (assignment.Lead == skillName) return SkillRole.Lead;
        if (assignment.Analysts.Contains(skillName)) return SkillRole.Analyst;
        if (assignment.Reviewers.Contains(skillName)) return SkillRole.Reviewer;
        if (assignment.Filter == skillName) return SkillRole.Filter;
        return null;
    }

    private static PlanArtifact? ResolvePlanArtifact(PipelineContext pipeline) =>
        pipeline.TryGet<PlanArtifact>(ContextKeys.PlanArtifact, out var artifact) ? artifact : null;

    private static List<SkillObservation> ApplyConfidenceThreshold(
        List<SkillObservation> parsed, string skillName, ILogger logger)
    {
        var result = new List<SkillObservation>(parsed.Count);
        foreach (var obs in parsed)
        {
            if (obs.Blocking && obs.Confidence < 70)
            {
                logger.LogInformation(
                    "Skill {Skill}: blocking observation '{Concern}' downgraded to non-blocking (confidence {Confidence} < 70)",
                    skillName, obs.Concern, obs.Confidence);
                result.Add(obs with { Blocking = false });
            }
            else
            {
                result.Add(obs);
            }
        }
        return result;
    }

    private static void StorePlanArtifactIfPlanLead(
        string skillName, SkillRole? assignedRole, PipelineContext pipeline,
        IReadOnlyList<SkillObservation> observations)
    {
        if (assignedRole != SkillRole.Lead) return;
        if (!pipeline.TryGet<PipelinePhase>(ContextKeys.CurrentPhase, out var phase) ||
            phase != PipelinePhase.Plan) return;
        var artifact = new PlanArtifact(skillName, observations, DateTimeOffset.UtcNow);
        pipeline.Set(ContextKeys.PlanArtifact, artifact);
    }

    private CommandResult? DetectBlockingFollowUp(
        IReadOnlyList<SkillObservation> parsed, string skillName,
        RoleSkillDefinition role, IReadOnlyList<RoleSkillDefinition> roles, int round,
        PipelineContext pipeline)
    {
        var blocking = parsed.FirstOrDefault(o => o.Blocking);
        if (blocking is null) return null;

        var targetRole = roles.FirstOrDefault(r =>
            r.Name != skillName &&
            r.Triggers.Any(t => t.Contains(
                blocking.Concern.ToString(), StringComparison.OrdinalIgnoreCase)));
        if (targetRole is null) return null;

        // Cap chain depth so a stuck blocking-cycle (e.g. skill A requests B; B can't fix the
        // root cause, requests A back; repeat) cannot burn LLM calls indefinitely. Convergence
        // check runs in the queue afterwards. Default 3, overridable via ProjectSkills.Discussion.MaxRounds.
        var maxRounds = ResolveMaxRounds(pipeline);
        if (round >= maxRounds)
        {
            Logger.LogInformation(
                "{Skill} blocking observation '{Concern}' would request {Target}, but skill-round cap reached (round {Round} >= max {Max}). Suppressing follow-up.",
                skillName, blocking.Concern, targetRole.Name, round, maxRounds);
            return null;
        }

        if (IsImmediatePingPong(pipeline, targetRole.Name, skillName))
        {
            Logger.LogInformation(
                "{Skill} blocking observation '{Concern}' would request {Target}, but {Target} just requested {Skill} in the previous round. Suppressing immediate ping-pong.",
                skillName, blocking.Concern, targetRole.Name, targetRole.Name, skillName);
            return null;
        }

        RecordSwitchSkillSummoner(pipeline, skillName, targetRole.Name);

        var nextRound = round + 1;
        return CommandResult.OkAndContinueWith(
            $"{role.DisplayName} has blocking concern ({blocking.Concern}), requesting {targetRole.DisplayName}",
            PipelineCommand.SkillRound(SkillRoundCommandName, targetRole.Name, nextRound),
            PipelineCommand.SkillRound(SkillRoundCommandName, skillName, nextRound),
            PipelineCommand.Simple(CommandNames.ConvergenceCheck));
    }

    private static int ResolveMaxRounds(PipelineContext pipeline)
    {
        if (pipeline.TryGet<SkillConfig>(ContextKeys.ProjectSkills, out var cfg) && cfg is not null)
            return cfg.Discussion.MaxRounds;
        return 3;
    }

    /// <summary>
    /// Returns true when the proposed target was the skill that summoned the current one
    /// in the immediately preceding switch — i.e. an immediate A→B→A bounce. Tracked via
    /// ContextKeys.SwitchSkillLastSummoner so cross-skill cycles break in O(1) regardless
    /// of round number.
    /// </summary>
    private static bool IsImmediatePingPong(PipelineContext pipeline, string proposedTarget, string currentSkill)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SwitchSkillLastSummoner, out var summoners) || summoners is null)
            return false;
        return summoners.TryGetValue(currentSkill, out var summoner)
            && summoner.Equals(proposedTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static void RecordSwitchSkillSummoner(PipelineContext pipeline, string summoner, string summoned)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SwitchSkillLastSummoner, out var summoners) || summoners is null)
            summoners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        summoners[summoned] = summoner;
        pipeline.Set(ContextKeys.SwitchSkillLastSummoner, summoners);
    }

    /// <summary>
    /// p0147b: routes the runtime-emitted execution-limit / execution-error
    /// observations (if any) into the shared pipeline observation list, the
    /// same way regular LLM observations flow. Round handlers call this once
    /// per <see cref="ISkillCallRuntime.ExecuteAsync"/> dispatch so silent
    /// skill drops surface in the final summary.
    /// </summary>
    internal static void BufferRuntimeObservations(
        PipelineContext pipeline, string skillName, int round, SkillCallResult result)
    {
        if (result.RuntimeObservations.Count == 0) return;
        var buffer = new SkillRoundBuffer(
            skillName, round, result.RuntimeObservations.ToList(), null, null);
        DispatchBuffer(pipeline, buffer);
    }

    private static void DispatchBuffer(PipelineContext pipeline, SkillRoundBuffer buffer)
    {
        if (pipeline.TryGet<List<SkillRoundBuffer>>(
                ContextKeys.DeferredBuffers, out var deferred) && deferred is not null)
        {
            lock (deferred) deferred.Add(buffer);
            return;
        }

        ApplyBufferToContext(pipeline, buffer);
    }

    /// <summary>
    /// Merges a buffer into the shared pipeline context. Assigns sequential observation IDs
    /// based on the current observation list state, then appends observations, the discussion
    /// entry, and any structured output. Caller is responsible for ordering when invoked
    /// against the same pipeline from multiple buffers.
    /// </summary>
    internal static void ApplyBufferToContext(PipelineContext pipeline, SkillRoundBuffer buffer)
    {
        if (buffer.Observations.Count > 0)
            ApplyObservations(pipeline, buffer.Observations);

        if (buffer.DiscussionEntry is not null)
            ApplyDiscussionEntry(pipeline, buffer.DiscussionEntry);

        if (buffer.StructuredOutput is not null)
            ApplyStructuredOutput(pipeline, buffer.SkillName, buffer.StructuredOutput);
    }

    private static void ApplyObservations(
        PipelineContext pipeline, IReadOnlyList<SkillObservation> parsed)
    {
        if (!pipeline.TryGet<List<SkillObservation>>(
                ContextKeys.SkillObservations, out var observations) || observations is null)
            observations = [];

        var nextId = observations.Count > 0 ? observations.Max(o => o.Id) + 1 : 1;
        foreach (var obs in parsed)
        {
            observations.Add(obs with { Id = nextId++ });
        }
        pipeline.Set(ContextKeys.SkillObservations, observations);
    }

    private static void ApplyDiscussionEntry(PipelineContext pipeline, DiscussionEntry entry)
    {
        if (!pipeline.TryGet<List<DiscussionEntry>>(
                ContextKeys.DiscussionLog, out var log) || log is null)
            log = [];
        log.Add(entry);
        pipeline.Set(ContextKeys.DiscussionLog, log);
    }

    private static void ApplyStructuredOutput(
        PipelineContext pipeline, string skillName, string output)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SkillOutputs, out var outputs) || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[skillName] = output;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);
    }

    private static string RenderObservationsAsText(List<SkillObservation> observations)
    {
        if (observations.Count == 0) return "No observations.";

        return string.Join("\n\n", observations.Select(o =>
        {
            var blocking = o.Blocking ? " [BLOCKING]" : "";
            var loc = o.DisplayLocation;
            var location = loc != "General" ? $" ({loc})" : "";
            return $"**{o.Concern}{blocking}** [{o.Severity}] (confidence: {o.Confidence}){location}\n" +
                   $"{o.Description}\n" +
                   (string.IsNullOrWhiteSpace(o.Suggestion) ? "" : $"→ {o.Suggestion}");
        }));
    }

    private async Task<CommandResult> ExecuteStructuredRoundAsync(
        string skillName, RoleSkillDefinition role, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        var orch = role.Orchestration!;

        pipeline.TryGet<Dictionary<string, string>>(
            ContextKeys.SkillOutputs, out var skillOutputs);
        var upstreamSnapshot = skillOutputs is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(skillOutputs, StringComparer.OrdinalIgnoreCase);

        var (domainStable, domainVariable) = BuildDomainSectionParts(pipeline);
        var upstreamContext = upstreamContextBuilder.Build(orch.Role, pipeline, upstreamSnapshot);
        var outputInstruction = _instructionBuilder.Build(orch);
        var existingTests = ResolveExistingTests(pipeline);

        var (systemPrompt, userPrefix, userSuffix) = promptBuilder.BuildStructuredPromptParts(
            role, domainStable, domainVariable, upstreamContext, outputInstruction, existingTests);

        if (orch.Role == OrchestrationRole.Gate)
            return await ExecuteGateRoundAsync(
                skillName, role, orch, systemPrompt, userPrefix, userSuffix, pipeline, cancellationToken);

        var agent = pipeline.Get<AgentConfig>(ContextKeys.AgentConfig);
        var combinedUser = string.IsNullOrEmpty(userSuffix) ? userPrefix : $"{userPrefix}\n\n{userSuffix}";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, combinedUser),
        };
        // p0142: structured (non-gate) Lead/Reviewer rounds dispatch via runtime.
        var runtimeRequest = new SkillCallRequest
        {
            SkillName = skillName,
            Role = role.Role ?? "investigator",
            Phase = MapPhase(pipeline),
            PromptParts = messages,
            ToolSet = Array.Empty<AITool>(),
            AgentConfig = agent,
            TaskType = TaskType.Primary,
            PipelineName = ResolvePipelineName(pipeline)
        };
        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);
        var runtimeResult = await _skillCallRuntime.ExecuteAsync(runtimeRequest, costTracker, cancellationToken);
        // p0147b: runtime observations (execution-limit / execution-error)
        // surface even when the structured round short-circuits on a Failed*.
        BufferRuntimeObservations(pipeline, skillName, round: 0, runtimeResult);
        if (TranslateStructuredOutcome(runtimeResult, skillName, role) is { } earlyFail)
            return earlyFail;
        var responseText = runtimeResult.Output ?? string.Empty;

        Logger.LogInformation("{Emoji} {DisplayName} [{Role}]: structured round complete",
            role.Emoji, role.DisplayName, orch.Role);

        var buffer = new SkillRoundBuffer(skillName, 0, [], null, responseText);
        DispatchBuffer(pipeline, buffer);

        if (orch.Role == OrchestrationRole.Lead)
            pipeline.Set(ContextKeys.ConsolidatedPlan, responseText);

        return CommandResult.Ok($"{role.DisplayName} [{orch.Role}]: complete");
    }

    private async Task<CommandResult> ExecuteGateRoundAsync(
        string skillName, RoleSkillDefinition role, SkillOrchestration orch,
        string systemPrompt, string userPrefix, string userSuffix, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        // p0142 (closes deferred-3x debt from p0132a/b/c): the gate path stays
        // direct (GateRetryCoordinator owns the corrective-retry policy), but
        // each attempt now records cost inside a SkillCallScope so PerSkill-
        // Breakdown reflects per-attempt tokens. The runtime is NOT involved
        // here — its retry policy is mechanical (parse/validation), gate is
        // observation-driven, and conflating the two would lose semantics.
        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);
        GateCallOutcome outcome;
        using (var scope = costTracker.BeginCall(skillName, role.Role ?? "investigator", MapPhase(pipeline)))
        {
            outcome = await gateRetryCoordinator.ExecuteAsync(
                role, orch, systemPrompt, userPrefix, userSuffix, pipeline, cancellationToken,
                onResponse: costTracker.Track);
        }

        var buffer = new SkillRoundBuffer(skillName, 0, [], null, outcome.FinalResponseText);
        DispatchBuffer(pipeline, buffer);

        Logger.LogInformation("{Emoji} {DisplayName} [Gate]: {Message}",
            role.Emoji, role.DisplayName, outcome.Result.Message);

        return outcome.Result;
    }

    /// <summary>
    /// p0142 Discussion-rounds outcome→CommandResult policy:
    /// Ok + Incomplete are both acceptable (Incomplete still produces partial
    /// observations that downstream handlers tolerate); FailedParse / Failed-
    /// Validation / FailedRuntime collapse to CommandResult.Fail with the
    /// runtime's FailureReason. Returns null when the round may proceed.
    /// </summary>
    private CommandResult? TranslateDiscussionOutcome(
        SkillCallResult result, string skillName, RoleSkillDefinition role)
    {
        switch (result.Outcome)
        {
            case SkillCallOutcome.Ok:
                return null;
            case SkillCallOutcome.Incomplete:
                Logger.LogWarning(
                    "{Skill} ({Role}) discussion round returned Incomplete (limit: {Limit}) — partial observations will be used",
                    skillName, role.DisplayName, result.Cost.HitLimit ?? "unknown");
                return null;
            default:
                return CommandResult.Fail(
                    $"{role.DisplayName} ({skillName}): {result.Outcome} — {result.FailureReason ?? "no reason given"}");
        }
    }

    /// <summary>
    /// p0142 Structured (non-gate) outcome→CommandResult policy: Plan/Review
    /// rounds can't tolerate partial output (downstream gate-handler would
    /// reject), so only Ok proceeds; Incomplete + every Failed* short-circuit.
    /// </summary>
    private CommandResult? TranslateStructuredOutcome(
        SkillCallResult result, string skillName, RoleSkillDefinition role)
    {
        return result.Outcome == SkillCallOutcome.Ok
            ? null
            : CommandResult.Fail(
                $"{role.DisplayName} ({skillName}): {result.Outcome} — {result.FailureReason ?? "no reason given"}");
    }

    /// <summary>p0145+p0142: pipeline-name carrier for the future IToolKit pickup; null falls back to '*'.</summary>
    private static string? ResolvePipelineName(PipelineContext pipeline)
        => pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null;

    private static bool IsStructuredRound(RoleSkillDefinition role, PipelineContext pipeline) =>
        role.Orchestration is not null
        && pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var pipelineType)
        && pipelineType is not PipelineType.Discussion;

    private static string? ResolveExistingTests(PipelineContext pipeline) =>
        pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var map) && map is not null
            ? ProjectMapPromptRenderer.RenderExistingTests(map)
            : null;

    /// <summary>
    /// p0132a: map the triage <see cref="PipelinePhase"/> from context
    /// (Plan/Review/Final) into the per-skill <see cref="SkillExecutionPhase"/>
    /// surface used by the cost-tracker. Falls back to Discuss when no
    /// phase is set (legacy discussion-mode rounds).
    /// </summary>
    private static SkillExecutionPhase MapPhase(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<PipelinePhase>(ContextKeys.CurrentPhase, out var phase))
            return SkillExecutionPhase.Discuss;
        return phase switch
        {
            PipelinePhase.Plan => SkillExecutionPhase.Plan,
            PipelinePhase.Review => SkillExecutionPhase.Review,
            PipelinePhase.Final => SkillExecutionPhase.Synthesize,
            _ => SkillExecutionPhase.Discuss,
        };
    }
}
