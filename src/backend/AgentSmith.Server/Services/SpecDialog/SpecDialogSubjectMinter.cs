using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-4b0af: mints the one-line subject a conversation is headed with, from its opening
/// exchange, ONCE.
/// <para>
/// Asked for rather than derived: every short model-produced text a conversation owns rides its
/// proposal, which arrives turns later and never arrives at all for a conversation that only
/// asked a question — and deriving a heading from the assistant's prose would be a heuristic
/// over prose. The call is made on the summarization task type, which the model registry routes
/// to the small model, with an explicit output ceiling of its own, and it is shown the first
/// exchange only rather than the turn's context.
/// </para>
/// <para>
/// It swallows everything, the way the proposal review swallows its own: a heading is not worth
/// the reply the turn already produced. A refused or failed mint stores nothing and the heading
/// falls back to the first line the person wrote.
/// </para>
/// </summary>
public sealed class SpecDialogSubjectMinter(
    IChatClientFactory chatClients,
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    SpecDialogSessionRepository repository,
    ILogger<SpecDialogSubjectMinter> logger)
{
    /// <summary>A heading is a line, so the ceiling is a line's worth of tokens and not the
    /// registry's summarization budget, which is sized for summaries.</summary>
    private const int MaxOutputTokens = 128;

    private const string Instruction =
        "You are naming a design conversation, for a heading above it.\n"
        + "Answer with ONE short line naming what the conversation is ABOUT, and with nothing "
        + "else: no quotation marks, no code fence, no markdown, no list, no closing full stop.\n"
        + "Write it in the SAME LANGUAGE the conversation is written in, not in English.\n"        + "Keep it under 120 characters. Do not restate the question; name the subject.";

    /// <summary>
    /// Mints and stores the subject when this conversation has none and has just had its first
    /// assistant turn, between persisting that turn and sending the reply. A FAILED turn mints
    /// nothing: the subject is never re-minted, so a heading named after half an exchange stands.
    /// </summary>
    public async Task MintAsync(ConversationState state, SpecDialogTurnKind kind, string reply, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Subject is not null || state.ThreadId is null || kind is SpecDialogTurnKind.Failure) return;
        if (state.Transcript.Any(turn => turn.Role == TranscriptRole.Assistant)) return;
        if (state.Transcript.FirstOrDefault(turn => turn.Role == TranscriptRole.User) is not { } asked)
            return;
        try
        {
            await MintAsync(state, asked.Text, reply, ct);
        }
        // On the caller's token, not on the exception's type: an LLM NetworkTimeout arrives as a
        // TaskCanceledException on an uncancelled token, and the reply this turn already has must
        // not be lost to a heading.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                ex, "The subject of session {SessionId} could not be minted", state.JobId);
        }
    }

    private async Task MintAsync(
        ConversationState state, string asked, string reply, CancellationToken ct)
    {
        var agent = Agent(state.Project);
        var response = await chatClients.Create(agent, TaskType.Summarization).GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, Instruction),
                new ChatMessage(ChatRole.User, $"They asked:\n{asked}\n\nThe answer began:\n{reply}"),
            ],
            new ChatOptions { MaxOutputTokens = MaxOutputTokens }, ct);
        // A raw chat call outside a pipeline run is accounted by nothing else: the per-call cost
        // events carry a run id and this call has none, and the TURN's tracker lives on a
        // PipelineContext that never leaves ExecutePipelineUseCase. So it gets one of its own.
        var cost = new PipelineCostTracker(config: agent.Pricing);
        cost.Track(response);
        if (SpecDialogSubjectAdmission.Of(response.Text) is not { } subject)
        {
            logger.LogInformation(
                "The minted subject of session {SessionId} was not one line of prose and was discarded",
                state.JobId);
            return;
        }
        logger.LogInformation(
            "Minted the subject of session {SessionId} for {Cost}", state.JobId, cost.EstimateCostUsd());
        await StoreAsync(state, subject, ct);
    }

    /// <summary>
    /// The project's OWN agent configuration, so the mint runs on the provider, model registry
    /// and pricing this conversation's turns run on, rather than on a bare fabricated one.
    /// </summary>
    private AgentConfig Agent(string projectName) =>
        configLoader.LoadConfig(serverContext.ConfigPath).Projects
            .TryGetValue(projectName, out var project)
            ? project.Agent
            : throw new InvalidOperationException(
                $"Spec-dialog session is scoped to project '{projectName}', which is not in the config catalog.");

    /// <summary>Written straight to the row, and only while it still has none: the state was read
    /// before the turn ran, so the row is what says whether one was minted meanwhile.</summary>
    private async Task StoreAsync(ConversationState state, string subject, CancellationToken ct)
    {
        var session = await repository.GetOpenByThreadAsync(state.Platform, state.ThreadId!, ct);
        if (session is null || session.Subject is not null) return;
        session.Subject = subject;
        await repository.SaveAsync(ct);
    }
}
