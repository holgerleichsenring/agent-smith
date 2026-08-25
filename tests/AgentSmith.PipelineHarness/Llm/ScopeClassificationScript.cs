using AgentSmith.Application.Services.Scope;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// p0413a: the scope classifier's own script slot, kept OUT of the main FIFO.
/// Since the estimate is asked for on every ticketed run, that call is a fixed
/// prelude of the code pipeline rather than part of the conversation a preset
/// scripts — and a strict FIFO would hand it the reply the test wrote for the
/// derivation, shifting every later slot by one. A preset that cares about the
/// scope verdict scripts it here; every other preset gets the benign default,
/// which states no verdict and no estimate, so the run behaves exactly as it
/// did when the call was not made at all.
/// </summary>
internal sealed class ScopeClassificationScript
{
    private readonly Queue<ChatResponse> _replies = new();

    public void Enqueue(string text) =>
        _replies.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    /// <summary>True when these messages are the scope-classification call.</summary>
    public static bool Answers(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(m => m.Role == ChatRole.System
            && string.Equals(m.Text, RepoScopeSystemPrompt.Text, StringComparison.Ordinal));

    public ChatResponse Next() =>
        _replies.Count > 0
            ? _replies.Dequeue()
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, "{}"));
}
