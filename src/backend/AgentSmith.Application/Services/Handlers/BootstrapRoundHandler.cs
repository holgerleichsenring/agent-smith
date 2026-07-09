using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
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
    ISandboxFileReaderFactory readerFactory,
    IRunContextAccessor runContext,
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

        var bundle = toolHostFactory.Create(sandbox, repo.LocalPath, context.RepoName, context.ContextName);
        var appliesTo = ResolveAppliesTo(pipeline);
        var (existingCtx, existingPrinciples) =
            await ReadExistingMetaFilesAsync(sandbox, context.ContextName, cancellationToken);
        var (system, user) = BootstrapPromptFactory.Build(
            role, repo, projectMap, context.ContextName, context.Workdir, appliesTo,
            existingCtx, existingPrinciples);
        var responseText = await CallSkillAsync(
            context, role, system, user, bundle.Tools, pipeline, cancellationToken);

        PersistOutput(context, context.SkillName, role, responseText);
        var changes = bundle.GetChanges();
        var decisions = bundle.GetDecisions();
        if (decisions.Count > 0) pipeline.AppendDecisions(decisions);

        // p0193-fix: context.yaml is written via write_context_yaml (its own
        // sandbox Step), so it never shows up in bundle.GetChanges() (which only
        // tracks the FilesystemToolHost writes, i.e. coding-principles.md). Verify
        // the artifact directly on the sandbox so a skipped/failed context.yaml
        // write FAILS loudly instead of the run reporting a silent success.
        var (ctxPath, _) = BootstrapPromptFactory.ResolveTargetPaths(context.ContextName);
        var contextYamlWritten =
            await FileExistsAsync(sandbox, ctxPath, cancellationToken);
        logger.LogInformation(
            "{Emoji} {DisplayName} [Bootstrap]: {Count} file(s) written, {Decisions} decision(s), context.yaml={CtxWritten}",
            role.Emoji, role.DisplayName, changes.Count, decisions.Count, contextYamlWritten);

        if (!contextYamlWritten)
            return CommandResult.Fail(
                $"BootstrapRound: skill '{context.SkillName}' did not produce {ctxPath} "
                + "— the write_context_yaml tool was not called or failed. context.yaml is required.");
        return changes.Count == 0
            ? CommandResult.Fail(
                $"BootstrapRound: skill '{context.SkillName}' did not call write_file "
                + "(0 changes). coding-principles.md not produced.")
            : CommandResult.Ok($"{role.DisplayName} [Bootstrap]: {changes.Count} file(s) written");
    }

    private async Task<bool> FileExistsAsync(ISandbox sandbox, string path, CancellationToken ct)
    {
        var reader = readerFactory.Create(sandbox);
        var content = await reader.TryReadAsync(path, ct);
        return !string.IsNullOrEmpty(content);
    }

    // p0202d: read the operator's existing context.yaml + coding-principles.md
    // so the producer merges (preserve + backfill) instead of regenerating
    // from source and clobbering. Both null on cold-init → generate-from-scratch.
    private async Task<(string? ContextYaml, string? Principles)> ReadExistingMetaFilesAsync(
        ISandbox sandbox, string contextName, CancellationToken ct)
    {
        var (ctxPath, principlesPath) = BootstrapPromptFactory.ResolveTargetPaths(contextName);
        var reader = readerFactory.Create(sandbox);
        var existingCtx = await reader.TryReadAsync(ctxPath, ct);
        var existingPrinciples = await reader.TryReadAsync(principlesPath, ct);
        return (existingCtx, existingPrinciples);
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
        var roleName = role.Role ?? "producer";
        using var _ = costTracker.BeginCall(
            context.SkillName, roleName, SkillExecutionPhase.Bootstrap, context.RepoName);
        using var _scope = runContext.BeginCallScope(
            roleName, SkillExecutionPhase.Bootstrap.ToString(), context.RepoName);
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

    // p0161d: per-phase applies_to wins if present; otherwise the prompt
    // factory falls back to its per-context PrimaryLanguage line (p0161a D4).
    private static string? ResolveAppliesTo(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.PhaseAppliesTo, out var appliesTo)
            && !string.IsNullOrWhiteSpace(appliesTo)
            ? appliesTo
            : null;

    private static void PersistOutput(
        BootstrapRoundContext context, string skillName,
        RoleSkillDefinition role, string responseText)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SkillOutputs, out var outputs) || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[skillName] = responseText;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);

        AppendBootstrapOutput(pipeline, context.RepoName, context.ContextName, responseText);

        if (!pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var discussion) || discussion is null)
            discussion = [];
        discussion.Add(new DiscussionEntry(skillName, role.DisplayName, role.Emoji, Round: 1, responseText));
        pipeline.Set(ContextKeys.DiscussionLog, discussion);
    }

    // p0161d: writes the (repo, context) → markdown output trail used by
    // WriteRunResultHandler's init-mode fan-out. Empty contextName uses
    // "default" so legacy single-context runs land in a predictable slot.
    private static void AppendBootstrapOutput(
        PipelineContext pipeline, string repoName, string contextName, string output)
    {
        if (!pipeline.TryGet<Dictionary<string, Dictionary<string, string>>>(
                ContextKeys.BootstrapOutputs, out var byRepo) || byRepo is null)
            byRepo = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        if (!byRepo.TryGetValue(repoName, out var byContext))
        {
            byContext = new Dictionary<string, string>(StringComparer.Ordinal);
            byRepo[repoName] = byContext;
        }
        var key = string.IsNullOrEmpty(contextName) ? "default" : contextName;
        byContext[key] = output;
        pipeline.Set(ContextKeys.BootstrapOutputs, byRepo);
    }
}
