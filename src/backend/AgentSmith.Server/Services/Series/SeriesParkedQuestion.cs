using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-17-0207a: the question a member ticket's newest attempt is parked on — its checkpoint's, while it
/// is unanswered — with the job id an answer is delivered to.</summary>
public sealed class SeriesParkedQuestion(IRunCheckpointStore checkpoints)
{
    public async Task<SeriesTicketFact> WithAsync(SeriesTicketFact fact, PreviousAttempt latest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(latest);
        if (latest.Status != RunStatuses.WaitingForInput) return fact;
        if (await checkpoints.GetByRunIdAsync(latest.RunId, ct) is not { ResumedAt: null } checkpoint) return fact;
        var question = JsonSerializer.Deserialize<DialogQuestion>(checkpoint.QuestionJson);
        return question is null ? fact : fact with { PendingQuestion = question, DialogueJobId = checkpoint.DialogueJobId };
    }
}
