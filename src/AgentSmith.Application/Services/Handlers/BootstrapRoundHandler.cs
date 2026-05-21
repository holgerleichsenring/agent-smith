using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Producer-loop runtime for bootstrap skills. Builds a bootstrap-restricted
/// SandboxToolHost (writes limited to .agentsmith/*), runs a tool-bearing chat
/// call, and persists the skill's Markdown summary into SkillOutputs +
/// DiscussionLog so WriteRunResult and InitCommit pick everything up.
/// </summary>
public sealed class BootstrapRoundHandler(
    IChatClientFactory chatClientFactory,
    BootstrapToolHostFactory toolHostFactory,
    ILogger<BootstrapRoundHandler> logger) : ICommandHandler<BootstrapRoundContext>
{
    public async Task<CommandResult> ExecuteAsync(
        BootstrapRoundContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!TryResolveRole(pipeline, context.SkillName, out var role, out var roleError))
            return CommandResult.Fail(roleError);
        if (!pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo) || repo is null)
            return CommandResult.Fail("BootstrapRound: no Repository in pipeline context");

        var sandbox = ResolvePerRepoSandbox(pipeline, context.RepoName);
        if (sandbox is null)
            return CommandResult.Fail(
                $"BootstrapRound: no sandbox available for repo '{context.RepoName}' " +
                "(checked Sandboxes[RepoName] and legacy ContextKeys.Sandbox)");
        var projectMap = ResolvePerRepoProjectMap(pipeline, context.RepoName);
        if (projectMap is null)
            return CommandResult.Fail(
                $"BootstrapRound: no ProjectMap available for repo '{context.RepoName}' " +
                "(checked RepoProjectMaps[RepoName] and legacy ContextKeys.ProjectMap)");

        var bundle = toolHostFactory.Create(sandbox, repo.LocalPath);
        var (system, user) = BootstrapPromptFactory.Build(role, repo, projectMap);
        var responseText = await CallSkillAsync(
            context, role, system, user, bundle.Tools, pipeline, cancellationToken);

        PersistOutput(pipeline, context.SkillName, role, responseText);
        var changes = bundle.GetChanges();
        var decisions = bundle.GetDecisions();
        if (decisions.Count > 0) pipeline.AppendDecisions(decisions);
        logger.LogInformation(
            "{Emoji} {DisplayName} [Bootstrap]: {Count} file(s) written, {Decisions} decision(s)",
            role.Emoji, role.DisplayName, changes.Count, decisions.Count);
        return changes.Count == 0
            ? CommandResult.Fail(
                $"BootstrapRound: skill '{context.SkillName}' did not call WriteFile "
                + "(0 changes). context.yaml / coding-principles.md not produced.")
            : CommandResult.Ok($"{role.DisplayName} [Bootstrap]: {changes.Count} file(s) written");
    }

    private async Task<string> CallSkillAsync(
        BootstrapRoundContext context, RoleSkillDefinition role,
        string system, string user, IList<AITool> tools,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Primary);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Primary);
        var options = new ChatOptions { Tools = tools, MaxOutputTokens = maxTokens };
        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);
        using var _ = costTracker.BeginCall(
            context.SkillName, role.Role ?? "producer", SkillExecutionPhase.Bootstrap);
        var response = await chat.GetResponseAsync(
            [new(ChatRole.System, system), new(ChatRole.User, user)], options, cancellationToken);
        costTracker.Track(response);
        return response.Text ?? string.Empty;
    }

    private static bool TryResolveRole(
        PipelineContext pipeline, string skillName,
        out RoleSkillDefinition role, out string error)
    {
        role = null!; error = string.Empty;
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, out var roles) || roles is null)
        { error = "BootstrapRound: no AvailableRoles in pipeline context"; return false; }
        var found = roles.FirstOrDefault(r => r.Name == skillName);
        if (found is null) { error = $"BootstrapRound: skill '{skillName}' not found in AvailableRoles"; return false; }
        role = found;
        return true;
    }

    // p0158g: dispatch order — Sandboxes[RepoName] wins; legacy
    // ContextKeys.Sandbox is the back-compat fallback (single-repo runs +
    // pre-p0158g test fixtures that only seed the singular slot).
    private static ISandbox? ResolvePerRepoSandbox(PipelineContext pipeline, string repoName)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var dict) && dict is not null
            && dict.TryGetValue(repoName ?? string.Empty, out var perRepo))
            return perRepo;
        return pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var legacy) ? legacy : null;
    }

    private static ProjectMap? ResolvePerRepoProjectMap(PipelineContext pipeline, string repoName)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
                ContextKeys.RepoProjectMaps, out var dict) && dict is not null
            && dict.TryGetValue(repoName ?? string.Empty, out var perRepo))
            return perRepo;
        return pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var legacy) ? legacy : null;
    }

    private static void PersistOutput(
        PipelineContext pipeline, string skillName, RoleSkillDefinition role, string responseText)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SkillOutputs, out var outputs) || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[skillName] = responseText;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);
        if (!pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var discussion) || discussion is null)
            discussion = [];
        discussion.Add(new DiscussionEntry(skillName, role.DisplayName, role.Emoji, Round: 1, responseText));
        pipeline.Set(ContextKeys.DiscussionLog, discussion);
    }
}
