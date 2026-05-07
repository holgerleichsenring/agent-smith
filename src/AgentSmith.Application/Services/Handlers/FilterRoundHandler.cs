using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
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
/// Filter-role round: reduces the SkillObservations list (output_type[Filter] = List)
/// or synthesizes a final artifact (output_type[Filter] = Artifact). p0124: list-mode
/// is now batched against the model's max_output_tokens so any observation count
/// works without truncation. Per-batch failure preserves that batch's input
/// observations and a single Coverage-Incomplete observation is appended to the
/// final list when any batch failed.
/// </summary>
public sealed class FilterRoundHandler(
    IChatClientFactory chatClientFactory,
    ISkillPromptBuilder promptBuilder,
    ILogger<FilterRoundHandler> logger) : ICommandHandler<FilterRoundContext>
{
    public async Task<CommandResult> ExecuteAsync(
        FilterRoundContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var skillName = context.SkillName;
        pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);

        var role = ResolveRole(skillName, pipeline);
        if (role is null) return CommandResult.Fail($"Filter skill '{skillName}' not found");

        var outputForm = ResolveOutputForm(role);
        var observations = LoadObservations(pipeline);

        return outputForm == OutputForm.Artifact
            ? await ApplyArtifactAsync(skillName, role, observations, context.AgentConfig, pipeline, cancellationToken)
            : await ApplyListAsync(skillName, role, observations, context.AgentConfig, pipeline, cancellationToken);
    }

    private async Task<CommandResult> ApplyListAsync(
        string skillName, RoleSkillDefinition role, List<SkillObservation> observations,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (observations.Count == 0)
        {
            logger.LogInformation("{Skill} (Filter, list): no observations to filter", skillName);
            return CommandResult.Ok($"{skillName} (Filter): nothing to filter");
        }

        var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Primary);
        var batches = TokenBudgetBatcher.Split(observations, maxTokens);
        var filtered = new List<SkillObservation>();
        var failedBatchCount = 0;
        var unfilteredFromFailedBatches = 0;

        for (var i = 0; i < batches.Count; i++)
        {
            var batchResult = await FilterBatchAsync(
                role, batches[i], i + 1, batches.Count,
                agentConfig, pipeline, cancellationToken);
            if (batchResult is null)
            {
                filtered.AddRange(batches[i]);
                failedBatchCount++;
                unfilteredFromFailedBatches += batches[i].Count;
            }
            else
            {
                filtered.AddRange(batchResult);
            }
        }

        if (failedBatchCount > 0)
            filtered.Add(FilterCoverageObservationFactory.Build(
                skillName, failedBatchCount, batches.Count, unfilteredFromFailedBatches));

        var withIds = filtered.Select((o, i) => o with { Id = i + 1 }).ToList();
        pipeline.Set(ContextKeys.SkillObservations, withIds);

        logger.LogInformation(
            "{Skill} (Filter, list): {InCount} → {OutCount} observations across {Batches} batches ({Failed} failed)",
            skillName, observations.Count, withIds.Count, batches.Count, failedBatchCount);

        return CommandResult.Ok(
            $"{skillName} (Filter): {observations.Count} → {withIds.Count} observations "
            + $"({batches.Count} batches, {failedBatchCount} failed)");
    }

    private async Task<List<SkillObservation>?> FilterBatchAsync(
        RoleSkillDefinition role, IReadOnlyList<SkillObservation> batch,
        int batchIndex, int totalBatches,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var rendered = RenderForFilter(batch);
        var inputChars = rendered.Length;
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Primary);
        var budgetChars = (int)(maxTokens * 4 * 0.85);

        var (system, user) = BuildPrompt(role, rendered, OutputForm.List);
        var chat = chatClientFactory.Create(agentConfig, TaskType.Primary);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system),
            new(ChatRole.User, user),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(response);
        var responseText = response.Text ?? string.Empty;

        var reduced = ObservationParser.TryParseWithoutIds(responseText, role.Name, logger);

        logger.LogInformation(
            "Filter batch {Index}/{Total}: input={InputCount} obs (~{InputChars} chars), "
            + "expected output ≤{BudgetChars} chars, actual response={ActualChars} chars, "
            + "parsed={Survivors}",
            batchIndex, totalBatches, batch.Count, inputChars, budgetChars,
            responseText.Length, reduced?.Count ?? 0);

        return reduced;
    }

    private async Task<CommandResult> ApplyArtifactAsync(
        string skillName, RoleSkillDefinition role, List<SkillObservation> observations,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var rendered = RenderObservations(observations);
        var (system, user) = BuildPrompt(role, rendered, OutputForm.Artifact);
        var chat = chatClientFactory.Create(agentConfig, TaskType.Primary);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Primary);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, system),
            new(ChatRole.User, user),
        };
        var response = await chat.GetResponseAsync(messages,
            new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(response);
        var responseText = response.Text ?? string.Empty;

        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SkillOutputs, out var outputs) || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[skillName] = responseText;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);
        logger.LogInformation(
            "{Skill} (Filter, artifact): produced {Bytes} bytes", skillName, responseText.Length);
        return CommandResult.Ok($"{skillName} (Filter): produced artifact ({responseText.Length} bytes)");
    }

    private (string System, string User) BuildPrompt(
        RoleSkillDefinition role, string rendered, OutputForm outputForm)
    {
        var instruction = outputForm == OutputForm.Artifact
            ? "Synthesize the observations into a final report. Return text."
            : "Return a JSON array of the observations to keep — drop duplicates and false positives. Use the SkillObservation schema.";
        var (system, prefix, suffix) = promptBuilder.BuildStructuredPromptParts(
            role, rendered, string.Empty, string.Empty, instruction,
            existingTests: null, assignedRole: SkillRole.Filter, planArtifact: null);
        return (system, $"{prefix}\n\n{suffix}");
    }

    /// <summary>
    /// Filter-input shape: omits Details (long-form prose isn't filter signal) and
    /// Rationale (filter doesn't need the why-I-think-so for keep/drop decisions).
    /// Keeps the headline fields the filter actually uses to decide.
    /// </summary>
    internal static string RenderForFilter(IReadOnlyList<SkillObservation> observations)
    {
        if (observations.Count == 0) return "[]";
        var trimmed = observations.Select(o => new
        {
            id = o.Id,
            role = o.Role,
            concern = o.Concern.ToString(),
            severity = o.Severity.ToString(),
            confidence = o.Confidence,
            file = o.File,
            start_line = o.StartLine,
            api_path = o.ApiPath,
            schema_name = o.SchemaName,
            description = o.Description,
            suggestion = o.Suggestion,
        });
        return JsonSerializer.Serialize(trimmed, new JsonSerializerOptions { WriteIndented = false });
    }

    private static RoleSkillDefinition? ResolveRole(string skillName, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null)
            return null;
        return roles.FirstOrDefault(r => r.Name == skillName);
    }

    private static OutputForm ResolveOutputForm(RoleSkillDefinition role)
    {
        if (role.OutputContract?.OutputType.TryGetValue(SkillRole.Filter, out var form) == true)
            return form;
        return OutputForm.List;
    }

    private static List<SkillObservation> LoadObservations(PipelineContext pipeline)
    {
        if (pipeline.TryGet<List<SkillObservation>>(
                ContextKeys.SkillObservations, out var list) && list is not null)
            return list;
        return [];
    }

    private static string RenderObservations(IReadOnlyList<SkillObservation> observations)
    {
        if (observations.Count == 0) return "(no observations)";
        return string.Join("\n\n", observations.Select(o =>
            $"#{o.Id} [{o.Role}] {o.Concern} ({o.Severity}, confidence {o.Confidence}): {o.Description}"));
    }
}
