using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-3653: how many turns a master pass used, counted from its own transcript.
/// <para>
/// Microsoft.Extensions.AI exposes no iteration count on a <see cref="ChatResponse"/> — the
/// function-invoking client only logs when it reaches the ceiling. What the response does
/// carry is every message of the pass, so ASSISTANT messages are the honest handle: one per
/// model turn, plus however many a provider chose to split a turn into. The number is
/// therefore near-exact and biased high, and everything that renders it says so.
/// </para>
/// <para>
/// Deliberately not the tool-call count: parallel tool calls put several in one turn, so
/// counting them over-reports against a ceiling that bounds ITERATIONS.
/// </para>
/// </summary>
public static class MasterTurnCount
{
    public static int From(ChatResponse? response) =>
        response?.Messages is { Count: > 0 } messages
            ? messages.Count(message => message.Role == ChatRole.Assistant)
            : 0;
}
