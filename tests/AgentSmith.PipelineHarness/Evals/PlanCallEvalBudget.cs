namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0397a: the eval tier's own money fence. Every LiveLLM plan call must fit
/// the remaining budget BEFORE it is made — the worst case is deterministic
/// (estimated input + the full MaxOutputTokens cap at the model's rates), so a
/// runaway matrix can never spend more than <c>AGENTSMITH_EVAL_BUDGET_USD</c>
/// (default 5.00). Actual spend is computed from the returned usage and
/// reported per row. Prices are an EVAL-SCOPE table, deliberately separate
/// from the server's pricing surface: the eval must stay runnable from the
/// test tree alone, and an unknown model prices at the most expensive known
/// rate so the fence errs toward refusing, never toward overspending.
/// </summary>
public sealed class PlanCallEvalBudget
{
    private const string BudgetEnv = "AGENTSMITH_EVAL_BUDGET_USD";
    private const decimal DefaultBudgetUsd = 5.00m;

    // USD per million tokens (input, output).
    private static readonly (string Prefix, decimal In, decimal Out)[] Rates =
    [
        ("gpt-4.1", 2.00m, 8.00m),
        ("gpt-4o", 2.50m, 10.00m),
        ("claude-sonnet", 3.00m, 15.00m),
        ("claude-haiku", 1.00m, 5.00m),
        ("claude-opus", 15.00m, 75.00m),
    ];

    // Unknown models price as the most expensive known rate (fail-closed).
    private static readonly (decimal In, decimal Out) Ceiling = (15.00m, 75.00m);

    private decimal _spentUsd;

    public decimal BudgetUsd { get; } = ReadBudget();

    public decimal SpentUsd => _spentUsd;

    /// <summary>Worst case of a call: estimated input tokens (chars/4) plus the
    /// FULL output cap at the model's rates.</summary>
    public decimal WorstCaseUsd(string model, int promptChars, int maxOutputTokens)
    {
        var (inRate, outRate) = RatesFor(model);
        return (promptChars / 4m) * inRate / 1_000_000m
            + (decimal)maxOutputTokens * outRate / 1_000_000m;
    }

    /// <summary>True when the call fits the remaining budget.</summary>
    public bool Allows(string model, int promptChars, int maxOutputTokens) =>
        _spentUsd + WorstCaseUsd(model, promptChars, maxOutputTokens) <= BudgetUsd;

    /// <summary>Actual cost from returned usage; falls back to the chars/4
    /// input estimate when the provider reported no input count.</summary>
    public decimal RecordActual(string model, int promptChars, long? inputTokens, long? outputTokens)
    {
        var (inRate, outRate) = RatesFor(model);
        var input = inputTokens ?? (long)(promptChars / 4m);
        var cost = input * inRate / 1_000_000m + (outputTokens ?? 0) * outRate / 1_000_000m;
        _spentUsd += cost;
        return cost;
    }

    private static (decimal In, decimal Out) RatesFor(string model)
    {
        foreach (var (prefix, input, output) in Rates)
            if (model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return (input, output);
        return Ceiling;
    }

    private static decimal ReadBudget() =>
        decimal.TryParse(
            Environment.GetEnvironmentVariable(BudgetEnv),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var v) && v > 0
            ? v
            : DefaultBudgetUsd;
}
