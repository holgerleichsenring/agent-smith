using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// 2026-11-27-3532b: a series-led run's question takes no default at its deadline — its series master decides. The passed
/// deadline wakes the master once per run and question (<see cref="SeriesWake.Once"/>, <see cref="KeyOf"/>), naming the
/// default the run would have taken; once raised, a sweep writes nothing. A run whose deadline was handed to its master never
/// takes the default later, when the series no longer leads it: a person answers it on the ticket page then.
/// </summary>
public sealed class SeriesQuestionDeadline(ISeriesWakes wakes, TimeProvider clock)
{
    /// <summary>The once-key of the checkpoint's deadline wake.</summary>
    public static string KeyOf(RunCheckpointRecord checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return $"question-deadline:{checkpoint.RunId}:{checkpoint.QuestionId}";
    }

    /// <summary>Whether the series was handed the checkpoint's passed deadline.</summary>
    public static bool Handed(SeriesRunRecord? series, RunCheckpointRecord checkpoint) =>
        series is not null && series.Raised.Contains(KeyOf(checkpoint), StringComparer.Ordinal);

    public async Task WakeIfPassedAsync(RunCheckpointRecord checkpoint, SeriesRunRecord series, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (clock.GetUtcNow() < checkpoint.AnswerDeadlineAt || Handed(series, checkpoint)) return;
        var question = JsonSerializer.Deserialize<DialogQuestion>(checkpoint.QuestionJson);
        var text = $"the question its run is parked on passed its deadline ({checkpoint.AnswerDeadlineAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC) "
            + (question?.DefaultAnswer is { Length: > 0 } fallback ? $"unanswered; its default \"{fallback}\" was not taken" : "unanswered")
            + " — answer it with tell_ticket or ask the operator";
        await wakes.WakeAsync(checkpoint.Project, checkpoint.TicketId, new SeriesWake(SeriesWakeKinds.Run, text) { Once = KeyOf(checkpoint) }, ct);
    }
}
