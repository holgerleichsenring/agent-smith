using AgentSmith.Contracts.Turns;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-d854c: for the turn about to run, sets who is told its run id — the conversation's row — until the
/// handle is disposed. The grounding step reports it through <see cref="ITurnRunRecorderAccessor"/>.
/// </summary>
public sealed class SpecDialogTurnRunLink(
    IServiceScopeFactory scopes, ITurnRunRecorderAccessor runs, ILoggerFactory loggers)
{
    public IDisposable Observe(ConversationState state) =>
        runs.Observe(new SessionTurnRunRecorder(scopes, state, loggers.CreateLogger<SessionTurnRunRecorder>()));
}
