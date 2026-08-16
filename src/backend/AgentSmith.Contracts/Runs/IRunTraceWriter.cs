namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423: records what a run's model actually SAW — one entry per call, stored beside the
/// run record rather than in it, because prompts grow into the megabytes and would drag
/// the conversation behind every statistics query.
/// <para>
/// <see cref="IsEnabled"/> is asked BEFORE the content is composed, so an untraced run
/// pays nothing to compose a string it will not keep.
/// </para>
/// </summary>
public interface IRunTraceWriter
{
    bool IsEnabled { get; }

    /// <summary>
    /// Records one entry. <paramref name="label"/> names the kind of call ("prompt",
    /// "answer", "tool"); ordering within the run is the writer's concern, so callers
    /// never invent sequence numbers.
    /// </summary>
    Task WriteAsync(string runId, string label, string content, CancellationToken cancellationToken);
}
