using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: holds the one session a client talks to, and decides when that session can no
/// longer serve the history it is being handed. A session is append-only conversation state, so a
/// history that diverges from what was sent cannot be repaired — only replaced.
/// </summary>
internal sealed class CopilotSessionLease(
    ICopilotRuntime runtime,
    CopilotSessionRequest template,
    ILogger logger)
{
    private ICopilotSessionHandle? _session;
    private List<string> _sentDigests = [];

    /// <summary>How many messages the live session has already been told.</summary>
    internal int SentCount => _sentDigests.Count;

    /// <summary>Records what the session has now been sent.</summary>
    internal void Commit(List<string> digests) => _sentDigests = digests;

    /// <summary>
    /// Returns the session this history belongs to, rebuilding when the history no longer extends
    /// what was sent. A rebuilt session is DELETED, not merely disposed: disposal preserves the
    /// transcript on disk.
    /// </summary>
    internal async Task<ICopilotSessionHandle> AcquireForAsync(
        IReadOnlyList<ChatMessage> history,
        IReadOnlyList<string> digests,
        CancellationToken cancellationToken)
    {
        if (_session is not null && CopilotHistoryWatermark.Extends(_sentDigests, digests))
            return _session;

        if (_session is not null)
        {
            logger.LogDebug(
                "Copilot history diverged from the {Sent} message(s) already sent — rebuilding session {Session}.",
                _sentDigests.Count, _session.SessionId);
            var stale = _session.SessionId;
            await _session.DisposeAsync();
            _session = null;
            await runtime.DeleteSessionAsync(stale, cancellationToken);
        }

        var systemMessage = CopilotPromptRenderer.SystemMessageOf(history) ?? template.SystemMessage;
        _session = await runtime.CreateSessionAsync(template with { SystemMessage = systemMessage }, cancellationToken);
        _sentDigests = [];
        return _session;
    }
}
