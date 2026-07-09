using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
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
/// disposes to nothing). Returns the reply text; the router persists and
/// delivers it.
/// </summary>
public sealed class SpecDialogTurnRunner(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ExecutePipelineUseCase pipelineUseCase,
    ISourceScopeSandboxFactory sourceSandboxFactory,
    SpecDialogQuestionPump questionPump,
    SpecDialogPendingQuestions pendingQuestions,
    ILogger<SpecDialogTurnRunner> logger) : ISpecDialogTurnRunner
{
    public async Task<SpecDialogTurnResult> RunTurnAsync(
        ConversationState state, CancellationToken cancellationToken)
    {
        var project = ResolveProject(state.Project);
        var scopeRepos = ResolveScopeRepos(project, state.Scope);
        var sandboxes = scopeRepos.ToDictionary(
            r => r.Name, r => (ISandbox)sourceSandboxFactory.Create(project, r), StringComparer.Ordinal);

        var slot = new SpecDialogReplySlot();
        var request = new PipelineRequest(
            ProjectName: state.Project,
            PipelineName: PipelinePresets.SpecDialogName,
            Headless: true,
            Context: BuildSeeds(state, scopeRepos, sandboxes, slot));

        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pump = questionPump.PumpAsync(state, pumpCts.Token);
        try
        {
            var result = await pipelineUseCase.ExecuteAsync(request, serverContext.ConfigPath, cancellationToken);
            // CollectSpecDialogReply writes reply + outcome together; a failed
            // run left both empty and resolves to an answer-shaped failure note.
            return slot is { Reply: not null, Outcome: not null }
                ? new SpecDialogTurnResult(slot.Reply, slot.Outcome)
                : new SpecDialogTurnResult(ComposeFailureReply(state, result), new AnswerOutcome());
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

    private static Dictionary<string, object> BuildSeeds(
        ConversationState state, IReadOnlyList<RepoConnection> scopeRepos,
        Dictionary<string, ISandbox> sandboxes, SpecDialogReplySlot slot)
    {
        var primary = scopeRepos[0];
        return new Dictionary<string, object>
        {
            [ContextKeys.SpecDialogTranscript] = MapTranscript(state.Transcript),
            [ContextKeys.SpecDialogReplySlot] = slot,
            [ContextKeys.DialogueJobId] = state.JobId,
            [ContextKeys.Sandboxes] = (IReadOnlyDictionary<string, ISandbox>)sandboxes,
            [ContextKeys.SandboxRepos] = (IReadOnlyDictionary<string, string>)scopeRepos
                .ToDictionary(r => r.Name, r => r.Name, StringComparer.Ordinal),
            // The master addresses repos by name through the tool host; the
            // singular Repository slot only feeds prompt headers/log lines.
            [ContextKeys.Repository] = new Repository(
                new BranchName(primary.DefaultBranch ?? "main"), primary.Url ?? string.Empty),
        };
    }

    private static IReadOnlyList<SpecDialogTurn> MapTranscript(IReadOnlyList<TranscriptTurn> transcript) =>
        [.. transcript.Select(t => new SpecDialogTurn(
            t.Role == TranscriptRole.Assistant ? SpecDialogTurn.AssistantRole : SpecDialogTurn.UserRole,
            t.Text))];

    private string ComposeFailureReply(ConversationState state, CommandResult result)
    {
        logger.LogWarning(
            "Spec-dialog turn for session {SessionId} produced no reply: {Message}",
            state.JobId, result.Message);
        return $"This design turn failed before an answer was produced: {result.Message ?? "unknown error"}";
    }
}
