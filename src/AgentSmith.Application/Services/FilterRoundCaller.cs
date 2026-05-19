using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Filter-mode LLM dispatch helper for FilterRoundHandler. Owns the Filter
/// prompt shape (trimmed observation rendering, list-vs-artifact instruction),
/// SkillCallRequest construction with the explicit SkillExecutionPhase, runtime
/// invocation, and runtime-observation buffering. Keeps the handler an
/// orchestration shell.
/// </summary>
public sealed class FilterRoundCaller(
    ISkillPromptBuilder promptBuilder,
    ISkillCallRuntime skillCallRuntime,
    ISkillRoundBufferDispatcher bufferDispatcher,
    FilterRoundToolPolicy toolPolicy,
    ObservationParser observationParser)
{
    public async Task<List<SkillObservation>?> InvokeBatchAsync(
        RoleSkillDefinition role, IReadOnlyList<SkillObservation> batch,
        int batchIndex, int totalBatches, AgentConfig agentConfig,
        PipelineContext pipeline, ILogger logger, CancellationToken cancellationToken)
    {
        var result = await DispatchAsync(role, role.Name, RenderForFilter(batch),
            OutputForm.List, SkillExecutionPhase.Filter, agentConfig, pipeline, cancellationToken);
        if (result.Outcome is not SkillCallOutcome.Ok and not SkillCallOutcome.Incomplete)
        {
            logger.LogWarning("Filter batch {Index}/{Total}: {Outcome} ({Reason}) — failed",
                batchIndex, totalBatches, result.Outcome, result.FailureReason ?? "no reason");
            return null;
        }
        return observationParser.TryParseWithoutIds(result.Output ?? string.Empty, role.Name, logger);
    }

    public Task<SkillCallResult> InvokeArtifactAsync(
        RoleSkillDefinition role, string skillName, IReadOnlyList<SkillObservation> observations,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken) =>
        DispatchAsync(role, skillName, RenderArtifact(observations), OutputForm.Artifact,
            SkillExecutionPhase.Synthesize, agentConfig, pipeline, cancellationToken);

    private async Task<SkillCallResult> DispatchAsync(
        RoleSkillDefinition role, string skillName, string rendered, OutputForm outputForm,
        SkillExecutionPhase phase, AgentConfig agentConfig,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var instruction = outputForm == OutputForm.Artifact
            ? "Synthesize the observations into a final report. Return text."
            : "Return a JSON array of the observations to keep — drop duplicates and false positives. Use the SkillObservation schema.";
        var (system, prefix, suffix) = promptBuilder.BuildStructuredPromptParts(
            role, rendered, string.Empty, string.Empty, instruction,
            existingTests: null, assignedRole: SkillRole.Filter, planArtifact: null);
        var request = new SkillCallRequest
        {
            SkillName = skillName,
            Role = role.Role ?? "filter",
            Phase = phase,
            PromptParts = new List<ChatMessage>
            {
                new(ChatRole.System, system),
                new(ChatRole.User, $"{prefix}\n\n{suffix}"),
            },
            ToolSet = toolPolicy.GetTools(role, pipeline).ToList(),
            AgentConfig = agentConfig,
            TaskType = TaskType.Primary,
            PipelineName = pipeline.TryGet<string>(ContextKeys.PipelineName, out var pn) ? pn : null,
        };
        var result = await skillCallRuntime.ExecuteAsync(
            request, PipelineCostTracker.GetOrCreate(pipeline), cancellationToken);
        if (result.RuntimeObservations.Count > 0)
            bufferDispatcher.Dispatch(pipeline,
                new SkillRoundBuffer(skillName, Round: 0, result.RuntimeObservations.ToList(), null, null));
        return result;
    }

    private static string RenderArtifact(IReadOnlyList<SkillObservation> observations) =>
        observations.Count == 0 ? "(no observations)"
            : string.Join("\n\n", observations.Select(o =>
                $"#{o.Id} [{o.Role}] {o.Concern} ({o.Severity}, confidence {o.Confidence}): {o.Description}"));

    /// <summary>
    /// Filter-input shape: omits Details + Rationale (filter doesn't need the
    /// why-I-think-so for keep/drop). Keeps the headline fields the filter uses.
    /// </summary>
    internal static string RenderForFilter(IReadOnlyList<SkillObservation> observations)
    {
        if (observations.Count == 0) return "[]";
        var trimmed = observations.Select(o => new
        {
            id = o.Id, role = o.Role,
            concern = o.Concern.ToString(), severity = o.Severity.ToString(),
            confidence = o.Confidence, file = o.File, start_line = o.StartLine,
            api_path = o.ApiPath, schema_name = o.SchemaName,
            description = o.Description, suggestion = o.Suggestion,
        });
        return JsonSerializer.Serialize(trimmed, new JsonSerializerOptions { WriteIndented = false });
    }
}
