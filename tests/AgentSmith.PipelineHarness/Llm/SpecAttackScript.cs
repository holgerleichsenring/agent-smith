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
    // 2026-11-02-2b26d: the panel's reviewers call in parallel, so the slot is drawn from concurrently.
    private readonly System.Collections.Concurrent.ConcurrentQueue<ChatResponse> _replies = new();

    public void Enqueue(string text) => Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    /// <summary>2026-11-02-2b26d: any reply, a tool call among them — a finding is admitted only on a look of its own.</summary>
    public void Enqueue(ChatResponse reply) => _replies.Enqueue(reply);

    /// <summary>True when these messages are an attack reviewer's call — its prompt opens with the brief.</summary>
    public static bool Answers(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(m => m.Text?.StartsWith(SpecAttackPrompt.Brief, StringComparison.Ordinal) == true);

    public ChatResponse Next() =>
        _replies.TryDequeue(out var reply) ? reply : new ChatResponse(new ChatMessage(ChatRole.Assistant, Build));
}
