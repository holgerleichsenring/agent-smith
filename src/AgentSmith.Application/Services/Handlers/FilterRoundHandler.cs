using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
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
/// Filter-role round: takes the current SkillObservations list and either reduces it
/// (output_type[Filter] = List) or synthesizes a final artifact (output_type[Filter]
/// = Artifact). No veto — Filter is a downstream-of-everyone synthesizer, not a gate.
/// </summary>
public sealed class FilterRoundHandler(
    ILlmClientFactory llmClientFactory,
    ISkillPromptBuilder promptBuilder,
    ILogger<FilterRoundHandler> logger) : ICommandHandler<FilterRoundContext>
{
    public async Task<CommandResult> ExecuteAsync(
        FilterRoundContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        var pipeline = context.Pipeline;
        var skillName = context.SkillName;

        var role = ResolveRole(skillName, pipeline);
        if (role is null) return CommandResult.Fail($"Filter skill '{skillName}' not found");

        var outputForm = ResolveOutputForm(role);
        var observations = LoadObservations(pipeline);
        var (system, user) = BuildPrompt(role, observations, outputForm);

        var response = await llmClient.CompleteAsync(
            system, user, TaskType.Planning, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(response);

        return outputForm == OutputForm.Artifact
            ? ApplyArtifact(skillName, response.Text, pipeline)
            : ApplyList(skillName, response.Text, pipeline);
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

    private (string System, string User) BuildPrompt(
        RoleSkillDefinition role, IReadOnlyList<SkillObservation> observations, OutputForm outputForm)
    {
        var rendered = RenderObservations(observations);
        var instruction = outputForm == OutputForm.Artifact
            ? "Synthesize the observations into a final report. Return text."
            : "Return a JSON array of the observations to keep — drop duplicates and false positives. Use the SkillObservation schema.";
        var (system, prefix, suffix) = promptBuilder.BuildStructuredPromptParts(
            role, rendered, string.Empty, string.Empty, instruction,
            existingTests: null, assignedRole: SkillRole.Filter, planArtifact: null);
        return (system, $"{prefix}\n\n{suffix}");
    }

    private static string RenderObservations(IReadOnlyList<SkillObservation> observations)
    {
        if (observations.Count == 0) return "(no observations)";
        return string.Join("\n\n", observations.Select(o =>
            $"#{o.Id} [{o.Role}] {o.Concern} ({o.Severity}, confidence {o.Confidence}): {o.Description}"));
    }

    private CommandResult ApplyList(string skillName, string responseText, PipelineContext pipeline)
    {
        var reduced = ObservationParser.ParseWithoutIds(responseText, skillName, logger);
        var withIds = reduced.Select((o, i) => o with { Id = i + 1 }).ToList();
        pipeline.Set(ContextKeys.SkillObservations, withIds);
        logger.LogInformation(
            "{Skill} (Filter, list): reduced to {Count} observations", skillName, withIds.Count);
        return CommandResult.Ok($"{skillName} (Filter): {withIds.Count} observations after reduction");
    }

    private CommandResult ApplyArtifact(string skillName, string responseText, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(
                ContextKeys.SkillOutputs, out var outputs) || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[skillName] = responseText;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);
        logger.LogInformation(
            "{Skill} (Filter, artifact): produced {Bytes} bytes", skillName, responseText.Length);
        return CommandResult.Ok($"{skillName} (Filter): produced artifact ({responseText.Length} bytes)");
    }
}
