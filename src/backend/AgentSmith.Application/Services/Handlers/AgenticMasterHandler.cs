using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0179b: runs a master skill body (resolved from IPromptCatalog by name
/// — typically "coding-agent-master") in one agentic loop. The master
/// decides plan + execute + verify internally; no choreography handlers
/// are involved. Coding pipelines (fix-bug, add-feature, fix-no-test)
/// dispatch this handler instead of the
/// Triage→GeneratePlan→…→AgenticExecute chain.
/// </summary>
public sealed class AgenticMasterHandler(
    IAgenticLoopRunner loopRunner,
    IPromptCatalog prompts,
    IDecisionLogger decisionLogger,
    AgentSmithConfig config,
    IContextYamlSerializer contextYamlSerializer,
    IDialogueTransport? dialogueTransport,
    ILogger<AgenticMasterHandler> logger)
    : ICommandHandler<AgenticMasterContext>
{
    public async Task<CommandResult> ExecuteAsync(
        AgenticMasterContext context, CancellationToken cancellationToken)
    {
        var sandboxes = context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var defaultKey = sandboxes.Keys.First();

        var masterBody = prompts.Render(context.MasterSkillName, new Dictionary<string, string>
        {
            ["ProjectContextSection"] = BuildProjectContextSection(context.ProjectContext),
            ["CodingPrinciples"] = context.CodingPrinciples,
            ["CodeMapSection"] = BuildCodeMapSection(context.CodeMap),
            ["RepoNames"] = BuildRepoNamesSection(sandboxes.Keys),
        });

        logger.LogInformation(
            "Running master skill '{Skill}' for repo {Repo}",
            context.MasterSkillName, context.Repository.LocalPath);
        var runCommandTimeout = context.Pipeline.TryGet<int>(ContextKeys.RunCommandTimeoutSeconds, out var rct)
            ? rct : (int?)null;
        var fs = new FilesystemToolHost(
            sandboxes, defaultKey, context.Repository.LocalPath,
            runCommandTimeoutSeconds: runCommandTimeout);
        var log = new LogDecisionToolHost(decisionLogger, context.Repository.LocalPath);
        var human = new HumanToolHost(dialogueTransport);
        var credentials = new GetArtifactCredentialsToolHost(config.Registries);
        var writeContextYaml = new WriteContextYamlToolHost(sandboxes, defaultKey, contextYamlSerializer);

        var ticket = context.Pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) && t is not null
            ? t
            : null;
        var userPrompt = BuildUserPrompt(ticket, context.Repository, sandboxes.Keys);

        var request = new AgenticLoopRequest(
            AgentConfig: context.AgentConfig,
            TaskType: TaskType.Primary,
            SystemPrompt: masterBody,
            UserPrompt: userPrompt,
            Tools: AgenticToolSurface.ReadWriteWithHuman(
                fs, log, human, credentials: credentials, writeContextYaml: writeContextYaml));

        var loopResult = await loopRunner.RunAsync(request, cancellationToken);

        var costTracker = PipelineCostTracker.GetOrCreate(context.Pipeline);
        costTracker.Track(loopResult.Response);

        var changes = fs.GetChanges();
        var decisions = log.GetDecisions();

        context.Pipeline.Set(ContextKeys.CodeChanges, changes);
        context.Pipeline.Set(ContextKeys.RunDurationSeconds, (int)loopResult.Duration.TotalSeconds);

        if (decisions.Count > 0)
        {
            context.Pipeline.AppendDecisions(decisions);
        }

        logger.LogInformation(
            "Master skill '{Skill}' completed: {Count} files changed, {Decisions} decisions",
            context.MasterSkillName, changes.Count, decisions.Count);

        return CommandResult.Ok($"Master '{context.MasterSkillName}' completed: {changes.Count} files changed");
    }

    private static string BuildProjectContextSection(string? projectContext) =>
        string.IsNullOrWhiteSpace(projectContext)
            ? string.Empty
            : $"## Project Context\n{projectContext}\n";

    private static string BuildCodeMapSection(string? codeMap) =>
        string.IsNullOrWhiteSpace(codeMap)
            ? string.Empty
            : $"## Code Map\n{codeMap}\n";

    // p0179h: list of repo (sandbox) names the master can address in this run.
    // Empty for 0-1 sandboxes (no prefix needed in the prompt body), bullet
    // list under a "Repositories in this run" heading for 2+. Coupled with
    // FilesystemToolHost.Route accepting the prefix in single-sandbox mode so
    // the master can use one consistent path convention.
    private static string BuildRepoNamesSection(IEnumerable<string> sandboxKeys)
    {
        var names = sandboxKeys.Where(k => !string.IsNullOrEmpty(k)).ToList();
        if (names.Count <= 1) return string.Empty;
        var bullets = string.Join("\n", names.Select(n => $"- `{n}`"));
        return $"## Repositories in this run\n{bullets}\n";
    }

    private static string BuildUserPrompt(Ticket? ticket, Repository repo, IEnumerable<string> sandboxKeys)
    {
        var ticketBlock = ticket is null
            ? "(No ticket attached — investigate the repository and proceed per pipeline goal.)"
            : $"""
                ## Ticket
                **ID:** {ticket.Id}
                **Title:** {ticket.Title}
                **Description:** {ticket.Description}
                **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}
                """;

        var keys = string.Join(", ", sandboxKeys);
        return $"""
            {ticketBlock}

            ## Working Repository
            **Path:** {repo.LocalPath}
            **Branch:** {repo.CurrentBranch}
            **Sandbox keys:** {keys}

            Investigate the repository, plan your change, implement it, and verify
            it (build + tests). Use the available tools — read_file, grep_in_tree,
            edit, write_file, run_command, log_decision, ask_human. When you are
            done, stop calling tools and summarise what changed.
            """;
    }
}
