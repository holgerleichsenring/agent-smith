namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// All message types exchanged over Redis Streams between agent containers and the dispatcher.
/// </summary>
public enum BusMessageType
{
    Progress,
    Detail,
    Question,
    Done,
    Error,
    Answer
}

/// <summary>
/// A message published to or consumed from a Redis Stream.
/// Agent → Dispatcher: Progress, Detail, Question, Done, Error
/// Dispatcher → Agent: Answer
/// </summary>
public sealed record BusMessage
{
    public required BusMessageType Type { get; init; }
    public required string JobId { get; init; }

    /// <summary>Progress step index (1-based). Only set for Progress messages.</summary>
    public int? Step { get; init; }

    /// <summary>Total pipeline steps. Only set for Progress messages.</summary>
    public int? Total { get; init; }

    /// <summary>Human-readable message text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Correlation ID for question/answer pairs.</summary>
    public string? QuestionId { get; init; }

    /// <summary>Human-readable step label. Set for Progress and Error messages.</summary>
    public string? StepName { get; init; }

    /// <summary>PR URL on successful completion. Only set for Done messages.</summary>
    public string? PrUrl { get; init; }

    /// <summary>Short summary of what changed. Only set for Done messages.</summary>
    public string? Summary { get; init; }

    /// <summary>User's answer content. Only set for Answer messages.</summary>
    public string? Content { get; init; }

    // --- Factories ---

    public static BusMessage Progress(string jobId, int step, int total, string text) => new()
    {
        Type = BusMessageType.Progress,
        JobId = jobId,
        Step = step,
        Total = total,
        Text = text
    };

    public static BusMessage Detail(string jobId, string text) => new()
    {
        Type = BusMessageType.Detail,
        JobId = jobId,
        Text = text
    };

    public static BusMessage Question(string jobId, string questionId, string text) => new()
    {
        Type = BusMessageType.Question,
        JobId = jobId,
        QuestionId = questionId,
        Text = text
    };

    public static BusMessage Done(string jobId, string? prUrl, string summary) => new()
    {
        Type = BusMessageType.Done,
        JobId = jobId,
        PrUrl = prUrl,
        Summary = summary,
        Text = summary
    };

    public static BusMessage Error(
        string jobId, string text,
        int step = 0, int total = 0, string stepName = "") => new()
    {
        Type = BusMessageType.Error,
        JobId = jobId,
        Text = text,
        Step = step > 0 ? step : null,
        Total = total > 0 ? total : null,
        StepName = string.IsNullOrEmpty(stepName) ? null : stepName
    };

    public static BusMessage Answer(string jobId, string questionId, string content) => new()
    {
        Type = BusMessageType.Answer,
        JobId = jobId,
        QuestionId = questionId,
        Content = content
    };
}
