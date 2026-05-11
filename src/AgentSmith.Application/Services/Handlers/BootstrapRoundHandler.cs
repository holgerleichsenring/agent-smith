using System.Text.Json;
using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
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
/// Producer-loop runtime for bootstrap skills (csharp/node/python/generic-bootstrap).
/// BootstrapDispatch routes a single matched skill here; the handler builds a
/// SandboxToolHost with the bootstrap-restricted PathWriteGuard (writes limited
/// to .agentsmith/context.yaml + .agentsmith/coding-principles.md), runs a
/// tool-bearing chat call, and persists the skill's Markdown summary into
/// SkillOutputs/DiscussionLog so WriteRunResult and InitCommit pick everything up.
///
/// Distinct from SkillRoundHandler: that path runs observation-only chats with no
/// tools attached, so a producer skill could never emit files there. The
/// scaffolding (ToolKit.BootstrapTools, PathWriteGuard.Bootstrap,
/// BootstrapOutputValidator, SkillCallRuntime) was in place since p0126/p0127
/// but never wired to a caller; this handler is that caller.
/// </summary>
public sealed class BootstrapRoundHandler(
    IChatClientFactory chatClientFactory,
    IDecisionLogger decisionLogger,
    ILogger<BootstrapRoundHandler> logger)
    : ICommandHandler<BootstrapRoundContext>
{
    public async Task<CommandResult> ExecuteAsync(
        BootstrapRoundContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;

        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, out var roles)
            || roles is null)
            return CommandResult.Fail("BootstrapRound: no AvailableRoles in pipeline context");
        var role = roles.FirstOrDefault(r => r.Name == context.SkillName);
        if (role is null)
            return CommandResult.Fail($"BootstrapRound: skill '{context.SkillName}' not found in AvailableRoles");
        if (!pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) || repository is null)
            return CommandResult.Fail("BootstrapRound: no Repository in pipeline context");
        if (!pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return CommandResult.Fail("BootstrapRound: no Sandbox in pipeline context");
        if (!pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var projectMap) || projectMap is null)
            return CommandResult.Fail("BootstrapRound: no ProjectMap in pipeline context");

        // Bootstrap writes are limited to .agentsmith/* via PathWriteGuard, which
        // never lives under any reasonable .gitignore — a no-op resolver is enough.
        // Repo-root and .git/ checks are still enforced by PathReadGuard itself.
        var readGuard = new PathReadGuard(NullGitIgnoreResolver.Instance, () => repository.LocalPath);
        var writeGuard = new PathWriteGuard(readGuard, SkillExecutionPhase.Bootstrap);
        var toolHost = new SandboxToolHost(
            sandbox, decisionLogger, dialogueTransport: null, jobId: null,
            repoPath: repository.LocalPath,
            readGuard: readGuard, writeGuard: writeGuard);

        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(toolHost.ReadFile),
            AIFunctionFactory.Create(toolHost.WriteFile),
            AIFunctionFactory.Create(toolHost.ListFiles),
            AIFunctionFactory.Create(toolHost.Grep),
            AIFunctionFactory.Create(toolHost.LogDecision),
        };

        var (systemPrompt, userPrompt) = BuildPrompts(role, repository, projectMap);

        var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Primary);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Primary);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };
        var options = new ChatOptions { Tools = tools, MaxOutputTokens = maxTokens };

        var costTracker = PipelineCostTracker.GetOrCreate(pipeline);
        ChatResponse response;
        using (var _ = costTracker.BeginCall(
            context.SkillName, role.Role ?? "producer", SkillExecutionPhase.Bootstrap))
        {
            response = await chat.GetResponseAsync(messages, options, cancellationToken);
            costTracker.Track(response);
        }
        var responseText = response.Text ?? string.Empty;

        PersistOutput(pipeline, context.SkillName, role, responseText);

        var changes = toolHost.GetChanges();
        var decisions = toolHost.GetDecisions();
        if (decisions.Count > 0)
            pipeline.AppendDecisions(decisions);

        logger.LogInformation(
            "{Emoji} {DisplayName} [Bootstrap]: {Count} file(s) written, {Decisions} decision(s)",
            role.Emoji, role.DisplayName, changes.Count, decisions.Count);

        if (changes.Count == 0)
            return CommandResult.Fail(
                $"BootstrapRound: skill '{context.SkillName}' did not call WriteFile " +
                "(0 changes). context.yaml / coding-principles.md not produced.");

        return CommandResult.Ok(
            $"{role.DisplayName} [Bootstrap]: {changes.Count} file(s) written");
    }

    private static (string System, string User) BuildPrompts(
        RoleSkillDefinition role, Repository repository, ProjectMap projectMap)
    {
        var systemPrompt = $"""
            ## Your Role
            {role.DisplayName}: {role.Description}

            ## Role-Specific Rules
            {role.Rules}
            """;

        var projectMapJson = JsonSerializer.Serialize(
            projectMap, new JsonSerializerOptions { WriteIndented = true });
        var userPrompt = $"""
            ## ProjectMap (from AnalyzeCode)

            ```json
            {projectMapJson}
            ```

            ## Repository
            - Branch: {repository.CurrentBranch.Value}
            - Local path: {repository.LocalPath}

            Read source files via your read-only tools as needed to ground claims
            (csproj/package.json, top-level Program.cs / index.ts, sample test).
            Then use the WriteFile tool to emit:
              - `.agentsmith/context.yaml`
              - `.agentsmith/coding-principles.md`
            After both writes succeed, return a short Markdown summary of the
            choices you made (per `output_schema: bootstrap`).
            """;

        return (systemPrompt, userPrompt);
    }

    private static void PersistOutput(
        PipelineContext pipeline, string skillName, RoleSkillDefinition role, string responseText)
    {
        if (!pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SkillOutputs, out var outputs)
            || outputs is null)
            outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        outputs[skillName] = responseText;
        pipeline.Set(ContextKeys.SkillOutputs, outputs);

        if (!pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var discussion)
            || discussion is null)
            discussion = [];
        discussion.Add(new DiscussionEntry(
            skillName, role.DisplayName, role.Emoji, Round: 1, responseText));
        pipeline.Set(ContextKeys.DiscussionLog, discussion);
    }

    private sealed class NullGitIgnoreResolver : IGitIgnoreResolver
    {
        public static readonly NullGitIgnoreResolver Instance = new();
        public bool IsIgnored(string fullPath, string repoPath) => false;
    }
}
