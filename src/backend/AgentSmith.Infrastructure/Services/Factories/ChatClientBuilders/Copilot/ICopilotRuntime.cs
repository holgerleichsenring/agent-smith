namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: the seam between the adapter and the GitHub Copilot runtime.
/// The other providers are reached over HTTP and can be tested through an injected
/// <c>HttpMessageHandler</c>; the Copilot SDK talks to a bundled CLI process instead,
/// so the testable seam has to be owned here. <see cref="CopilotRuntime"/> is the only
/// implementation that touches the SDK; the tests drive scripted
/// <see cref="CopilotSessionEvent"/> sequences through a stand-in.
/// </summary>
public interface ICopilotRuntime
{
    /// <summary>Opens a session. The runtime process starts on the first call, never at construction.</summary>
    Task<ICopilotSessionHandle> CreateSessionAsync(CopilotSessionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a session's persisted state. Disposing a session PRESERVES its conversation,
    /// planning state and artifacts on disk so it can be resumed, so a rebuild that only
    /// disposed would leave the abandoned transcript — including any credential a sensitive
    /// tool returned — behind for the life of the sandbox.
    /// </summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken);
}

/// <summary>What a session is opened with. Every field is fixed at creation by the SDK.</summary>
/// <param name="Model">The model id the role routes to, or null for the runtime's default.</param>
/// <param name="ReasoningEffort">low / medium / high / xhigh / max, or null.</param>
/// <param name="SystemMessage">
/// Replaces Copilot's own system prompt rather than appending to it — appending would put the
/// coding agent's instructions in front of the run's own and there would be no parity left.
/// </param>
/// <param name="SeatToken">
/// The per-agent seat. Copilot rejects org-owned PATs, so the token is a person's, which is why
/// it belongs to the session and not to the shared runtime.
/// </param>
/// <param name="ToolNames">
/// The allowlist. Empty means the model reaches no tool at all — the posture 2026-09-07-d5f2
/// ships; 2026-09-23-4722a fills it with the names of the run's own tools. A built-in name is
/// never added.
/// </param>
public sealed record CopilotSessionRequest(
    string? Model,
    string? ReasoningEffort,
    string? SystemMessage,
    string? SeatToken,
    IReadOnlyList<string> ToolNames);
