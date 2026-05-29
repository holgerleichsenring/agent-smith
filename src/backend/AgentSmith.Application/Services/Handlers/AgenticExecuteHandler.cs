using System.Diagnostics;
using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Application.Services.Prompts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Executes the plan via the shared <see cref="IAgenticLoopRunner"/> (p0177
/// extracted the FunctionInvokingChatClient loop out of this handler). The
/// handler still owns the tool-host construction + post-call collection of
/// changes and decisions; the loop runner owns the chat completion call.
/// </summary>
public sealed class AgenticExecuteHandler(
    IAgenticLoopRunner loopRunner,
    AgentPromptBuilder promptBuilder,
    IDecisionLogger decisionLogger,
    IDialogueTransport? dialogueTransport,
    ILogger<AgenticExecuteHandler> logger)
    : ICommandHandler<AgenticExecuteContext>
{

    public async Task<CommandResult> ExecuteAsync(
        AgenticExecuteContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing plan with {Steps} steps...", context.Plan.Steps.Count);

        var sandboxes = context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes);
        var defaultKey = sandboxes.Keys.First();
        var fs = new FilesystemToolHost(sandboxes, defaultKey, context.Repository.LocalPath);
        var log = new LogDecisionToolHost(decisionLogger, context.Repository.LocalPath);
        var human = new HumanToolHost(dialogueTransport);

        var systemPrompt = promptBuilder.BuildExecutionSystemPrompt(
            context.CodingPrinciples, context.CodeMap, context.ProjectContext);
        var verifyNotes = context.Pipeline.TryGet<string>(ContextKeys.VerifyNotes, out var vn) ? vn : null;
        var perKeyLanguages = context.Pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
            ContextKeys.RepoProjectMaps, out var maps) && maps is not null
            ? maps.ToDictionary(kv => kv.Key, kv => kv.Value.PrimaryLanguage, StringComparer.Ordinal)
            : null;
        var appliesTo = context.Pipeline.TryGet<string>(ContextKeys.PhaseAppliesTo, out var phaseAppliesTo)
            && !string.IsNullOrWhiteSpace(phaseAppliesTo)
            ? phaseAppliesTo
            : null;
        var userPrompt = promptBuilder.BuildExecutionUserPrompt(
            context.Plan, context.Repository, verifyNotes,
            contextKeys: sandboxes.Keys.ToList(),
            perKeyLanguages: perKeyLanguages,
            appliesTo: appliesTo);

        var request = new AgenticLoopRequest(
            AgentConfig: context.AgentConfig,
            TaskType: TaskType.Primary,
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            Tools: AgenticToolSurface.ReadWriteWithHuman(fs, log, human));

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
            "Agentic execution completed: {Count} files changed, {Decisions} decisions",
            changes.Count, decisions.Count);

        return CommandResult.Ok($"Agentic execution completed: {changes.Count} files changed");
    }
}
