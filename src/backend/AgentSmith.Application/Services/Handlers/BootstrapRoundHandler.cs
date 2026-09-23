using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
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
    BootstrapMetaFiles metaFiles,
    BootstrapPrinciplesTransfer principlesTransfer,
    BootstrapContextWriteVerdict contextWrite,
    BootstrapOutputRecorder outputRecorder,
    SandboxTargets sandboxTargets,
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

        var sandbox = sandboxTargets.OwnedForContext(pipeline, context.RepoName, context.ContextName);
        if (sandbox is null)
            return CommandResult.Fail(
                $"BootstrapRound: no sandbox available for repo '{context.RepoName}' " +
                $"context '{context.ContextName}' (no sandbox key belongs to this repo)");
        // 2026-09-04-0721: this context's own map first. RepoProjectMaps is keyed by SANDBOX,
        // so on a repository whose contexts share a toolchain image it describes the
        // representative's subtree — and this round writes a DIFFERENT context's context.yaml.
        var projectMap = ResolveContextProjectMap(pipeline, context.RepoName, context.ContextName)
                         ?? RepoOwnedProjectMap.In(pipeline, context.RepoName, sandboxTargets);
        if (projectMap is null)
            return CommandResult.Fail(
                $"BootstrapRound: no ProjectMap available for repo '{context.RepoName}' " +
                "(no ContextProjectMaps entry for this context, and no sandbox key it owns "
                + "carries a RepoProjectMaps entry)");

        var bundle = toolHostFactory.Create(sandbox, repo.LocalPath, context.RepoName, context.ContextName);
        var appliesTo = ResolveAppliesTo(pipeline);
        var existing = await metaFiles.ReadAsync(sandbox, context.ContextName, cancellationToken);
        if (existing.Error is not null) return CommandResult.Fail(existing.Error);
        // p0379: principles are authored gold — transfer the composed core+delta
        // (or preserve a ratified file) BEFORE the skill call; the skill then
        // writes facts (context.yaml) only. Pre-p0379 catalogs keep SkillWrites.
        var (_, principlesPath) = BootstrapPromptFactory.ResolveTargetPaths(context.ContextName);
        var transfer = await principlesTransfer.ApplyAsync(
            pipeline, sandbox, context.RepoName, context.ContextName, projectMap,
            principlesPath, existing.Principles, cancellationToken);
        if (transfer.Error is not null) return CommandResult.Fail(transfer.Error);
        // 2026-09-15-c6e9: the round's facts reach the init pull request, which is the one
        // artefact a human opens. Appended, not set: one round per component.
        pipeline.AppendBootstrapOutcome(new BootstrapRoundOutcome(
            context.RepoName, context.ContextName, transfer.Mode, transfer.Artefacts));
        var (system, user) = BootstrapPromptFactory.Build(
            role, repo, projectMap, context.ContextName, context.Workdir, appliesTo,
            existing.ContextYaml, existing.Principles, transfer.Mode);
        var responseText = await CallSkillAsync(
            context, role, system, user, bundle.Tools, pipeline, cancellationToken);

        outputRecorder.Record(context, role, responseText);
        var changes = bundle.GetChanges();
        var decisions = bundle.GetDecisions();
        if (decisions.Count > 0) pipeline.AppendDecisions(decisions);

        // 2026-08-26-167c: the round asks the TOOL what this round did, and the
        // sandbox only whether the file it reported is really there. Asking the
        // sandbox alone made a re-init whose every write was refused report green.
        var (ctxPath, _) = BootstrapPromptFactory.ResolveTargetPaths(context.ContextName);
        var outcome = bundle.GetContextWrite();
        var onDisk = await metaFiles.ExistsAsync(sandbox, ctxPath, cancellationToken);
        logger.LogInformation(
            "{Emoji} {DisplayName} [Bootstrap]: {Count} file(s) written, {Decisions} decision(s), context.yaml written={CtxWritten} on-disk={OnDisk}",
            role.Emoji, role.DisplayName, changes.Count, decisions.Count, outcome.Written, onDisk);

        if (contextWrite.Failure(context.SkillName, ctxPath, outcome, onDisk) is { } failure)
            return CommandResult.Fail(failure);
        // p0379: in transfer/preserve mode the principles file is framework-owned,
        // so a round with zero write_file changes is the expected success shape.
        if (transfer.Mode != PrinciplesMode.SkillWrites)
            return CommandResult.Ok(BootstrapPrinciplesOutcome.Sentence(
                transfer, role.DisplayName, existing.RetiredRenamed));
        return changes.Count == 0
            ? CommandResult.Fail(
                $"BootstrapRound: skill '{context.SkillName}' did not call write_file "
                + "(0 changes). principles.md not produced.")
            : CommandResult.Ok(BootstrapPrinciplesOutcome.SkillWroteThem(
                transfer, role.DisplayName, changes.Count, existing.RetiredRenamed));
    }

    private async Task<string> CallSkillAsync(
        BootstrapRoundContext context, RoleSkillDefinition role,
        string system, string user, IList<AITool> tools,
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.ContextGeneration);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.ContextGeneration);
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

    private static ProjectMap? ResolveContextProjectMap(
        PipelineContext pipeline, string repoName, string contextName) =>
        pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyDictionary<string, ProjectMap>>>(
            ContextKeys.ContextProjectMaps, out var byRepo)
        && byRepo is not null
        && byRepo.TryGetValue(repoName ?? string.Empty, out var byContext)
        && byContext.TryGetValue(contextName, out var map)
            ? map
            : null;

    // p0161d: per-phase applies_to wins if present; otherwise the prompt
    // factory falls back to its per-context PrimaryLanguage line (p0161a D4).
    private static string? ResolveAppliesTo(PipelineContext pipeline) =>
        pipeline.TryGet<string>(ContextKeys.PhaseAppliesTo, out var appliesTo)
            && !string.IsNullOrWhiteSpace(appliesTo)
            ? appliesTo
            : null;
}
