using AgentSmith.Application.Services.Specs;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// The spec review's own script slot, kept OUT of the main FIFO — the same treatment
/// <see cref="ScopeClassificationScript"/> gets, for the same reason.
/// <para>
/// The review runs on every derived spec, so it is a fixed prelude of the code pipeline
/// rather than part of the conversation a preset scripts. A strict FIFO would hand it the
/// reply the test wrote for the master and shift every later slot by one. A preset that
/// cares about the review scripts it here; every other preset gets the benign default — an
/// empty answer, which leaves every criterion decidable, so the run behaves exactly as it
/// did when the call was not made at all.
/// </para>
/// </summary>
internal sealed class SpecReviewScript
{
    private readonly Queue<ChatResponse> _replies = new();

    public void Enqueue(string text) =>
        _replies.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    /// <summary>True when these messages are the spec-review call.</summary>
    public static bool Answers(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(m => m.Role == ChatRole.User
            && (m.Text?.StartsWith(SpecReviewPrompt.Marker, StringComparison.Ordinal) ?? false));

    public ChatResponse Next() =>
        _replies.Count > 0
            ? _replies.Dequeue()
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, "[]"));
}
