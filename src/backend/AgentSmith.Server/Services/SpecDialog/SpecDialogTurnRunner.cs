using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315b: runs ONE design-conversation turn as an in-process spec-dialog
/// pipeline run. Seeds the session's transcript, the lazy read-only source
/// sandboxes for the active scope, the dialogue job id (ask_human) and the
/// reply slot into the run via PipelineRequest.Context; pumps the master's
/// questions into the thread while the run is live; owns the sandboxes'
/// lifetime (disposed when the turn ends — a sandbox that served no read
/// disposes to nothing). Returns the reply as kept and as shown on the session's platform;
/// the router persists the first and delivers the second.
/// <para>
/// 2026-09-13-ed5a: the project's declared TEMPLATES join that set. The scope's repos come
/// from the session row; the templates come off the resolved project — the two columns
/// SpecDialogSessionMapper rebuilds ActiveScope from carry no template, so a field there
/// would be dropped before the first turn.
/// </para>
/// </summary>
public sealed class SpecDialogTurnRunner(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ExecutePipelineUseCase pipelineUseCase,
    ISourceScopeSandboxFactory sourceSandboxFactory,
    SpecDialogTemplateScopes templateScopes,
    SpecDialogQuestionPump questionPump,
    SpecDialogPendingQuestions pendingQuestions,
    DashboardReadingChannel reading,
    ILogger<SpecDialogTurnRunner> logger) : ISpecDialogTurnRunner
{
    public async Task<SpecDialogTurnResult> RunTurnAsync(
        ConversationState state, CancellationToken cancellationToken)
    {
        var project = ResolveProject(state.Project);
        var scopeRepos = ResolveScopeRepos(project, state.Scope);
        var templates = templateScopes.Open(project);
        var sandboxes = SpecDialogTurnSeeds.Sandboxes(
            scopeRepos, r => sourceSandboxFactory.Create(project, r), templates);

        var slot = new SpecDialogReplySlot();
        var request = new PipelineRequest(
            ProjectName: state.Project,
            PipelineName: PipelinePresets.SpecDialogName,
            Headless: true,
            Context: SpecDialogTurnSeeds.Build(state, scopeRepos, sandboxes, slot));

        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pump = questionPump.PumpAsync(state, pumpCts.Token);
        // Set before the pipeline runs, so both the scope repos and the templates it opens
        // report through the flow; any run that sets none reports nothing.
        using var observing = reading.Observe(state);
        try
        {
            var result = await pipelineUseCase.ExecuteAsync(request, serverContext.ConfigPath, cancellationToken);
            // CollectSpecDialogReply writes reply + outcome together; a failed
            // run left both empty and resolves to an answer-shaped failure note.
            // The stamp rides the outcome because the scopes below are gone by filing time.
            return slot is { Reply: not null, Outcome: not null }
                ? SpecDialogTurnResult.On(
                    state.Platform, slot.Reply, templateScopes.Stamp(slot.Outcome, project, templates))
                : SpecDialogTurnResult.On(
                    state.Platform, ComposeFailureReply(state, result), new AnswerOutcome());
        }
        finally
        {
            pumpCts.Cancel();
            await pump;
            pendingQuestions.Clear(state.JobId);
            foreach (var sandbox in sandboxes.Values)
                await sandbox.DisposeAsync();
        }
    }

    private ResolvedProject ResolveProject(string projectName)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (config.Projects.TryGetValue(projectName, out var project)) return project;
        throw new InvalidOperationException(
            $"Spec-dialog session is scoped to project '{projectName}', which is not in the config catalog.");
    }

    // The active scope's repo names filter the project's repo set; an empty
    // scope list means the whole project (the resolver stored all repos at
    // session start, but stay tolerant of older rows).
    private static IReadOnlyList<RepoConnection> ResolveScopeRepos(
        ResolvedProject project, ActiveScope? scope)
    {
        if (scope is null || scope.Repos.Count == 0) return project.Repos;
        var wanted = new HashSet<string>(scope.Repos, StringComparer.OrdinalIgnoreCase);
        var matched = project.Repos.Where(r => wanted.Contains(r.Name)).ToList();
        return matched.Count > 0 ? matched : project.Repos;
    }

    private string ComposeFailureReply(ConversationState state, CommandResult result)
    {
        logger.LogWarning(
            "Spec-dialog turn for session {SessionId} produced no reply: {Message}",
            state.JobId, result.Message);
        return $"This design turn failed before an answer was produced: {result.Message ?? "unknown error"}";
    }
}
