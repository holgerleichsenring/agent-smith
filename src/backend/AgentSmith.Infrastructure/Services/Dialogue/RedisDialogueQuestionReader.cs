using AgentSmith.Contracts.Dialogue;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Dialogue;

/// <summary>
/// Reads the newest question on a run's outbound dialogue stream (job:{id}:out), in the shape
/// <see cref="RedisDialogueTransport"/> writes it. The stream carries other message types too,
/// so a short window from the tail is scanned newest first.
/// </summary>
public sealed class RedisDialogueQuestionReader(IConnectionMultiplexer redis) : IDialogueQuestionReader
{
    private const int TailWindow = 50;
    private const char ChoiceSeparator = '|';

    public async Task<DialogQuestion?> LatestAsync(string dialogueJobId, CancellationToken cancellationToken)
    {
        var entries = await redis.GetDatabase().StreamRangeAsync(
            $"job:{dialogueJobId}:out", "-", "+", TailWindow, Order.Descending);
        return entries
            .Select(e => e.Values.ToDictionary(v => (string)v.Name!, v => (string?)v.Value))
            .Where(IsQuestion)
            .Select(ToQuestion)
            .FirstOrDefault();
    }

    private static bool IsQuestion(Dictionary<string, string?> fields) =>
        string.Equals(fields.GetValueOrDefault("type"), "question", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(fields.GetValueOrDefault("questionId"));

    private static DialogQuestion ToQuestion(Dictionary<string, string?> fields) => new(
        fields["questionId"]!,
        Enum.TryParse<QuestionType>(fields.GetValueOrDefault("questionType"), true, out var type)
            ? type : QuestionType.Confirmation,
        fields.GetValueOrDefault("text") ?? string.Empty,
        NullIfEmpty(fields.GetValueOrDefault("context")),
        Choices(fields.GetValueOrDefault("choices")),
        NullIfEmpty(fields.GetValueOrDefault("defaultAnswer")),
        TimeSpan.FromSeconds(double.TryParse(fields.GetValueOrDefault("timeoutSeconds"), out var s) ? s : 0));

    private static IReadOnlyList<DialogChoice>? Choices(string? joined) =>
        string.IsNullOrEmpty(joined)
            ? null
            : joined.Split(ChoiceSeparator).Select(label => new DialogChoice(label)).ToList();

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
