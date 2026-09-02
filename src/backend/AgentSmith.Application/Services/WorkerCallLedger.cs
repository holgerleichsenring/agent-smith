using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Workers;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-01-b0d7: the tokens and the USD figure an external agent CLI reported for the
/// calls it answered, accumulated in their OWN channel.
/// <para>
/// Apart from priced spend because no money is spent there. Pricing a worker call from
/// the model table produces a figure the run never paid; leaving it to fall through
/// produces "COST INCOMPLETE, no price for: sonnet" — a pricing alarm for a call that has
/// no price by design. The honest third option is this one: the CLI's number, labelled as
/// the CLI's, never added to accrued spend.
/// </para>
/// <para>
/// Not comparable to a provider call either. A trivial probe reported a third of a cent on
/// twenty-six thousand cache-creation tokens — the CLI's own system prompt and tool
/// schemas, charged per call, not this run's context.
/// </para>
/// </summary>
public sealed class WorkerCallLedger
{
    // Sorted so the rendered model name is stable across runs that call in any order.
    private readonly SortedSet<string> _models = new(StringComparer.OrdinalIgnoreCase);

    public int CallCount { get; private set; }
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public long CacheReadTokens { get; private set; }
    public long CacheCreationTokens { get; private set; }
    public decimal ReportedCostUsd { get; private set; }

    /// <summary>
    /// Adds one call. The model is taken from what the CLI named, never from the alias the
    /// agent was configured with — "sonnet" attributes nothing.
    /// </summary>
    public void Add(WorkerCallAccounting accounting)
    {
        ArgumentNullException.ThrowIfNull(accounting);
        CallCount++;
        InputTokens += accounting.InputTokens;
        OutputTokens += accounting.OutputTokens;
        CacheReadTokens += accounting.CacheReadTokens;
        CacheCreationTokens += accounting.CacheCreationTokens;
        ReportedCostUsd += accounting.ReportedCostUsd;
        if (!string.IsNullOrWhiteSpace(accounting.Model)) _models.Add(accounting.Model);
    }

    /// <summary>Raw token volume — real context read on any transport, so the token arm
    /// of the budget fence counts it. See <see cref="ReportedCostUsd"/> for the arm that
    /// deliberately does not bind.</summary>
    public long TotalTokens =>
        InputTokens + OutputTokens + CacheReadTokens + CacheCreationTokens;

    public string Models => _models.Count == 0 ? "unknown" : string.Join("+", _models);

    /// <summary>Null when no worker call was tracked, so a provider-only run renders none.</summary>
    public WorkerSpend? Snapshot() => CallCount == 0
        ? null
        : new WorkerSpend(Models, CallCount, InputTokens, OutputTokens,
            CacheReadTokens, CacheCreationTokens, ReportedCostUsd);

    public override string ToString() =>
        $"{CallCount} worker CLI calls · {TotalTokens} tokens "
        + $"({InputTokens} in, {OutputTokens} out, {CacheReadTokens} cache read, "
        + $"{CacheCreationTokens} cache create) · ${ReportedCostUsd:F4} as the CLI reports "
        + $"it, not billed here · {Models}";
}
