using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// 2026-09-01-b0d7: what ONE external-worker call cost, as the agent CLI itself reports
/// it — its token counts, its own USD figure, and the model id it names.
/// <para>
/// It travels on the response in its own channel rather than through the pricing table.
/// A subscription-answered call spends no money, so pricing its model name would invent a
/// figure; leaving it unpriced would raise the unpriced-model alarm for a call that has no
/// price BY DESIGN. Neither is the truth, so the CLI's own number is carried as the CLI's
/// own number.
/// </para>
/// </summary>
public sealed record WorkerCallAccounting(
    string Model,
    long InputTokens,
    long OutputTokens,
    long CacheReadTokens,
    long CacheCreationTokens,
    decimal ReportedCostUsd,
    int CliTurns)
{
    private const string ResponseKey = "agentsmith.worker_call";

    /// <summary>
    /// Marks the response as answered by a worker CLI and hangs its figures on it. The
    /// cached subset stays OUT of <see cref="UsageDetails.CachedInputTokenCount"/>: the
    /// CLI follows Anthropic semantics, where <c>input_tokens</c> already excludes cache
    /// reads, and the OpenAI cached-input path would both double-count the read and
    /// subtract it from billable input.
    /// </summary>
    public ChatResponse AttachTo(ChatResponse response)
    {
        response.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        response.AdditionalProperties[ResponseKey] = this;
        if (!string.IsNullOrWhiteSpace(Model)) response.ModelId = Model;
        response.Usage = new UsageDetails
        {
            InputTokenCount = InputTokens,
            OutputTokenCount = OutputTokens,
            AdditionalCounts = new AdditionalPropertiesDictionary<long>
            {
                ["cache_read_input_tokens"] = CacheReadTokens,
                ["cache_creation_input_tokens"] = CacheCreationTokens,
            },
        };
        return response;
    }

    /// <summary>
    /// Reads back what <see cref="AttachTo"/> wrote — null for every provider call, which
    /// is what keeps the two spend channels from ever being added together.
    /// </summary>
    public static WorkerCallAccounting? Of(ChatResponse response)
    {
        if (response.AdditionalProperties is not { } properties) return null;
        return properties.TryGetValue(ResponseKey, out object? value)
            ? value as WorkerCallAccounting
            : null;
    }
}
