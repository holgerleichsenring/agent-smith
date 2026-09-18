using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// 2026-09-17-042eh: the phase review's own script slot, kept OUT of the main FIFO, for the
/// reason <see cref="ScopeClassificationScript"/> states: the review is asked once per phase
/// of every code run, so a strict FIFO would hand it the reply a preset wrote for the master
/// and shift every later slot by one. A preset that cares scripts it here; every other preset
/// gets the benign empty array, which keeps no finding and splices nothing — the run behaves
/// exactly as it did before the step existed.
/// </summary>
public sealed class PhaseReviewScript
{
    private readonly Queue<ChatResponse> _replies = new();

    /// <summary>The opening of the review prompt, which nothing else says.</summary>
    public const string Marker = "You are reading the diff of ONE phase";

    public void Enqueue(string text) =>
        _replies.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    /// <summary>True when these messages are the phase-review call.</summary>
    public static bool Answers(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(m => m.Text?.Contains(Marker, StringComparison.Ordinal) == true);

    public ChatResponse Next() =>
        _replies.Count > 0
            ? _replies.Dequeue()
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, "[]"));
}
