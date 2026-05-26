using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Filter-role round: reduces the SkillObservations list (output_type[Filter] =
/// List, batched against max_output_tokens via <see cref="IFilterRoundBatcher"/>)
/// or synthesizes a final artifact (output_type[Filter] = Artifact). Per-batch
/// failure preserves that batch's inputs and appends a Coverage-Incomplete
/// observation so reviewers can see which batches the filter could not review.
/// </summary>
public sealed class FilterRoundHandler(
    IChatClientFactory chatClientFactory,
    IFilterRoundBatcher batcher,
    FilterRoundCaller caller,
    ILogger<FilterRoundHandler> logger) : ICommandHandler<FilterRoundContext>
{
    public async Task<CommandResult> ExecuteAsync(
        FilterRoundContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var skillName = context.SkillName;
        pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);

        if (!TryResolveRole(skillName, pipeline, out var role))
            return CommandResult.Fail($"Filter skill '{skillName}' not found");

        var observations = LoadObservations(pipeline);
        return ResolveOutputForm(role) == OutputForm.Artifact
            ? await ApplyArtifactAsync(skillName, role, observations, context.AgentConfig, pipeline, cancellationToken)
            : await ApplyListAsync(skillName, role, observations, context.AgentConfig, pipeline, cancellationToken);
    }

    private async Task<CommandResult> ApplyListAsync(
        string skillName, RoleSkillDefinition role, List<SkillObservation> observations,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (observations.Count == 0)
            return CommandResult.Ok($"{skillName} (Filter): nothing to filter");

        var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Primary);
        var batches = batcher.Split(observations, maxTokens);
        var filtered = new List<SkillObservation>();
        var failed = 0;
        var unfiltered = 0;

        for (var i = 0; i < batches.Count; i++)
        {
            var batchResult = await caller.InvokeBatchAsync(
                role, batches[i], i + 1, batches.Count, agentConfig, pipeline, logger, cancellationToken);
            if (batchResult is null) { filtered.AddRange(batches[i]); failed++; unfiltered += batches[i].Count; }
            else filtered.AddRange(batchResult);
        }

        if (failed > 0)
            filtered.Add(FilterCoverageObservationFactory.Build(skillName, failed, batches.Count, unfiltered));
        var withIds = filtered.Select((o, i) => o with { Id = i + 1 }).ToList();
        pipeline.Set(ContextKeys.SkillObservations, withIds);
        logger.LogInformation(
            "{Skill} (Filter, list): {In} → {Out} obs across {Batches} batches ({Failed} failed)",
            skillName, observations.Count, withIds.Count, batches.Count, failed);
        return CommandResult.Ok(
            $"{skillName} (Filter): {observations.Count} → {withIds.Count} observations "
            + $"({batches.Count} batches, {failed} failed)");
    }

    private async Task<CommandResult> ApplyArtifactAsync(
        string skillName, RoleSkillDefinition role, List<SkillObservation> observations,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var result = await caller.InvokeArtifactAsync(
            role, skillName, observations, agentConfig, pipeline, cancellationToken);
        if (result.Outcome is not SkillCallOutcome.Ok and not SkillCallOutcome.Incomplete)
            return CommandResult.Fail(
                $"{skillName} (Filter, artifact): {result.Outcome} — {result.FailureReason ?? "no reason"}");
        var responseText = result.Output ?? string.Empty;
        if (!pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SkillOutputs, out var outputs) || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[skillName] = responseText;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);
        logger.LogInformation("{Skill} (Filter, artifact): produced {Bytes} bytes", skillName, responseText.Length);
        return CommandResult.Ok($"{skillName} (Filter): produced artifact ({responseText.Length} bytes)");
    }

    private static bool TryResolveRole(string skillName, PipelineContext pipeline, out RoleSkillDefinition role)
    {
        role = null!;
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, out var roles) || roles is null)
            return false;
        var found = roles.FirstOrDefault(r => r.Name == skillName);
        if (found is null) return false;
        role = found;
        return true;
    }

    private static OutputForm ResolveOutputForm(RoleSkillDefinition role) =>
        role.OutputSchema switch { "diff" => OutputForm.Artifact, "bootstrap" => OutputForm.Artifact, _ => OutputForm.List };

    private static List<SkillObservation> LoadObservations(PipelineContext pipeline) =>
        pipeline.TryGet<List<SkillObservation>>(ContextKeys.SkillObservations, out var list) && list is not null
            ? list
            : [];
}
