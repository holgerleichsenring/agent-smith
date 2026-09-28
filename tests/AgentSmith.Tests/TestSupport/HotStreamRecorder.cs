using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Tests.TestSupport;

/// <summary>The hot dialogue stream beneath the durable transport: records the answers put on
/// it, and never answers a wait — which is what the stream does once its TTL has passed.</summary>
internal sealed class HotStreamRecorder : IDialogueTransport
{
    public List<(string JobId, DialogAnswer Answer)> Answers { get; } = [];

    public Task PublishQuestionAsync(string jobId, DialogQuestion question, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<DialogAnswer?> WaitForAnswerAsync(
        string jobId, string questionId, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromResult<DialogAnswer?>(null);

    public Task PublishAnswerAsync(string jobId, DialogAnswer answer, CancellationToken cancellationToken)
    {
        Answers.Add((jobId, answer));
        return Task.CompletedTask;
    }
}
