using AgentSmith.Application.Services.Specs.Review;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// 2026-10-13-8762g: the run-time spec review's reviewers answer from their own slot, kept OUT of the main
/// FIFO for the reason <see cref="SpecReviewScript"/> states: ReviewSpecs asks spec_review.reviewers fresh
/// calls per unbuilt spec of every code run, so a strict FIFO would hand them the replies a preset wrote for
/// the master. A preset that cares scripts it here; every other preset gets a BUILD with nothing to cut,
/// which revises nothing — the run behaves as it did before the step existed.
/// </summary>
public sealed class SpecAttackScript
{
    private const string Build = """{"verdict": "BUILD", "cuts": [], "findings": []}""";
    private readonly Queue<ChatResponse> _replies = new();

    public void Enqueue(string text) =>
        _replies.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    /// <summary>True when these messages are an attack reviewer's call — its prompt opens with the brief.</summary>
    public static bool Answers(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(m => m.Text?.StartsWith(SpecAttackPrompt.Brief, StringComparison.Ordinal) == true);

    public ChatResponse Next() =>
        _replies.Count > 0 ? _replies.Dequeue() : new ChatResponse(new ChatMessage(ChatRole.Assistant, Build));
}
