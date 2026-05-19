using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.SkillRounds;

/// <summary>
/// p0147d: Resolves PipelineContext fields (project context, domain rules,
/// code map, discussion log, plan artifact, existing tests, assigned role)
/// and delegates to <see cref="ISkillPromptBuilder"/> for the actual prompt
/// assembly. Returns the (system, user-prefix, user-suffix) triple so the
/// caller can attach cache markers + flatten into ChatMessages.
/// </summary>
public sealed class PromptComposer(
    ISkillPromptBuilder promptBuilder,
    StructuredOutputInstructionBuilder instructionBuilder,
    IUpstreamContextBuilder upstreamContextBuilder) : IPromptComposer
{
    public (string SystemPrompt, string UserPrefix, string UserSuffix) ComposeDiscussion(
        RoleSkillDefinition role, ISkillPromptStrategy strategy,
        string skillName, int round, PipelineContext pipeline)
    {
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        pipeline.TryGet<string>(ContextKeys.DomainRules, out var domainRules);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var discussionLog);
        var existingTests = ResolveExistingTests(pipeline);
        var assignedRole = ResolveAssignedRole(skillName, pipeline);
        var planArtifact = ResolvePlanArtifact(pipeline);
        var (domainStable, domainVariable) = strategy.BuildDomainSectionParts(pipeline);
        return promptBuilder.BuildDiscussionPromptParts(
            role, domainStable, domainVariable, projectContext, domainRules, codeMap,
            (IReadOnlyList<DiscussionEntry>)(discussionLog ?? []),
            round, existingTests, assignedRole, planArtifact);
    }

    public (string SystemPrompt, string UserPrefix, string UserSuffix) ComposeStructured(
        RoleSkillDefinition role, ISkillPromptStrategy strategy, PipelineContext pipeline)
    {
        var orch = role.Orchestration!;
        pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SkillOutputs, out var skillOutputs);
        var upstreamSnapshot = skillOutputs is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(skillOutputs, StringComparer.OrdinalIgnoreCase);
        var (domainStable, domainVariable) = strategy.BuildDomainSectionParts(pipeline);
        var upstreamContext = upstreamContextBuilder.Build(orch.Role, pipeline, upstreamSnapshot);
        var outputInstruction = instructionBuilder.Build(orch);
        var existingTests = ResolveExistingTests(pipeline);
        return promptBuilder.BuildStructuredPromptParts(
            role, domainStable, domainVariable, upstreamContext, outputInstruction, existingTests);
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

    private static string? ResolveExistingTests(PipelineContext pipeline) =>
        pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var map) && map is not null
            ? ProjectMapPromptRenderer.RenderExistingTests(map)
            : null;
}
