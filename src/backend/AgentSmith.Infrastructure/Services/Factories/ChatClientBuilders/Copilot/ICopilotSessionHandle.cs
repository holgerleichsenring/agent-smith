using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: one live Copilot conversation, narrowed to what the adapter uses.
/// </summary>
public interface ICopilotSessionHandle : IAsyncDisposable
{
    string SessionId { get; }

    /// <summary>Subscribes to the session's event stream until the returned handle is disposed.</summary>
    IDisposable Subscribe(Action<CopilotSessionEvent> handler);

    /// <summary>
    /// Queues a prompt.
    ///
    /// There is no non-billable variant here on purpose. SendMessageItem.Billable would suppress
    /// the premium-request charge — which the doctor's reachability probe would like — but it is
    /// reachable only through <c>Rpc.SendAsync</c>, which the SDK marks GHCP001 "for evaluation
    /// purposes only and is subject to change or removal". A preflight saving is not worth a
    /// production dependency on an API the vendor has reserved the right to delete, so the probe
    /// costs one premium request per copilot agent per doctor run, and that is stated rather than
    /// bought on credit.
    /// </summary>
    Task SendAsync(string prompt, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the session's accumulated usage. The figures are session-wide totals, so a single
    /// call's consumption is the difference between two readings — see <see cref="CopilotUsage"/>.
    /// </summary>
    Task<CopilotUsage> ReadUsageAsync(CancellationToken cancellationToken);

    Task AbortAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 2026-09-23-4722a: answers one pending tool call, which resumes the turn. Exactly one of
    /// <paramref name="result"/> and <paramref name="error"/> is given.
    /// </summary>
    Task RespondToToolAsync(string requestId, string? result, string? error, CancellationToken cancellationToken);
}

/// <summary>
/// 2026-09-23-4722a: one tool as the session is told about it — a name, a description and a JSON
/// Schema, and deliberately NO body. A declaration the runtime can execute is one it WILL execute,
/// which would put the tool loop inside the session; a declaration without one is left pending for
/// us, which is what keeps the loop in agent-smith where every decorator can re-enter it.
/// </summary>
public sealed record CopilotToolDefinition(string Name, string Description, JsonElement Parameters);

/// <summary>
/// A reading of the session's usage accumulators. These are TOTALS since the session opened,
/// never per-call figures: <c>LastCallInputTokens</c> looks like the per-call answer but is
/// documented as the most recent MAIN-AGENT api call, which stops being the turn as soon as one
/// turn spans several model calls. The adapter therefore subtracts two readings.
/// </summary>
public readonly record struct CopilotUsage(
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    double PremiumRequestCost,
    double? NanoAiu)
{
    public static CopilotUsage Zero => new(0, 0, 0, 0, null);

    /// <summary>What was consumed between an earlier reading and this one.</summary>
    public CopilotUsage Since(CopilotUsage earlier) => new(
        InputTokens - earlier.InputTokens,
        OutputTokens - earlier.OutputTokens,
        CacheReadTokens - earlier.CacheReadTokens,
        PremiumRequestCost - earlier.PremiumRequestCost,
        NanoAiu is null || earlier.NanoAiu is null ? NanoAiu : NanoAiu - earlier.NanoAiu);
}

/// <summary>
/// The session events the adapter reacts to, in its own vocabulary so a test can script a
/// conversation without the SDK or a Copilot seat.
/// </summary>
public abstract record CopilotSessionEvent
{
    /// <summary>
    /// A complete assistant message. <paramref name="ToolRequestCount"/> is how many tools it
    /// asked for, which is how the adapter knows how many external-tool requests to expect — a
    /// session that is waiting on a pending tool call never goes idle.
    /// </summary>
    public sealed record AssistantMessage(string Text, int ToolRequestCount = 0) : CopilotSessionEvent;

    /// <summary>An incremental chunk, raised only when the session was created streaming.</summary>
    public sealed record AssistantDelta(string Text) : CopilotSessionEvent;

    /// <summary>
    /// The model called a tool the runtime will NOT execute, because the declaration carries no
    /// body. 2026-09-23-4722a answers these; until then no tool is ever registered, so none arrive.
    /// </summary>
    public sealed record ExternalToolRequested(
        string RequestId, string ToolCallId, string ToolName, string ArgumentsJson) : CopilotSessionEvent;

    /// <summary>A pending tool request was resolved by someone other than us.</summary>
    public sealed record ExternalToolCompleted(string RequestId) : CopilotSessionEvent;

    /// <summary>The turn is finished and it is safe to send again.</summary>
    public sealed record Idle : CopilotSessionEvent;

    /// <summary>The session failed; the call faults with this message.</summary>
    public sealed record Failed(string Message) : CopilotSessionEvent;
}
