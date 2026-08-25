namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-25-c9c7: bounds the reject-and-rewrite exchange on write_context_yaml.
/// <para>
/// A refusal comes back as a tool result and the model re-emits inside its agentic
/// loop. A document it cannot make valid would otherwise be re-sent until the
/// iteration cap — the shape of the p0385/p0386 analyzer failure. After
/// <see cref="Limit"/> refusals for one context the budget stops inviting another
/// attempt and says the write is abandoned; the round then fails on its own
/// missing-context.yaml guard instead of consuming the loop.
/// </para>
/// <para>
/// Lifetime is the tool host's, which is one round — a context that is accepted
/// clears its own tally, so a later legitimate rewrite starts from a full budget.
/// </para>
/// </summary>
public sealed class ContextWriteRejectionBudget
{
    public const int Limit = 5;

    private readonly Dictionary<string, int> _refusals = new(StringComparer.Ordinal);

    public string Reject(string context, string defect)
    {
        var count = _refusals.TryGetValue(context, out var previous) ? previous + 1 : 1;
        _refusals[context] = count;
        return count > Limit
            ? $"Error: write_context_yaml has refused context '{context}' {Limit} times and will "
              + "not accept another attempt in this round. Stop calling it for this context and "
              + $"report the unresolved defect: {defect}"
            : $"Error: {defect}\nFix exactly what is named above and call write_context_yaml "
              + $"again (refusal {count} of {Limit} for context '{context}').";
    }

    public void Accepted(string context) => _refusals.Remove(context);

    public bool IsExhausted(string context) =>
        _refusals.TryGetValue(context, out var count) && count > Limit;
}
