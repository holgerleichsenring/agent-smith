using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Tests.TestSupport;

/// <summary>The run's live dialogue stream, as a test sets it: the latest question per run.</summary>
internal sealed class StubQuestionReader : IDialogueQuestionReader
{
    public Dictionary<string, DialogQuestion> Latest { get; } = [];

    public Task<DialogQuestion?> LatestAsync(string dialogueJobId, CancellationToken cancellationToken) =>
        Task.FromResult(Latest.GetValueOrDefault(dialogueJobId));
}
