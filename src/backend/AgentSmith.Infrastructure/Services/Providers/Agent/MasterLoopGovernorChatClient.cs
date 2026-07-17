using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// p0341c: the master loop's in-pass governor — a <see cref="DelegatingChatClient"/> that
/// sits BELOW UseFunctionInvocation, so the FunctionInvokingChatClient re-enters it on
/// every tool iteration. Two levers, the harness analogue of how an interactive coding
/// harness stringently drives a model:
/// <list type="bullet">
///   <item>WITHIN-pass money fence — before each iteration it consults the live budget;
///     an exhausted budget throws <see cref="MasterBudgetExhaustedException"/> so a single
///     runaway pass stops on money, not only the 200-iteration anti-runaway ceiling. After
///     each iteration it feeds the response usage back to the budget estimator.</item>
///   <item>Ledger reminder — every N iterations, and on drift (K consecutive iterations
///     with reads but no edit), it appends the current ledger + a done-discipline line as a
///     synthetic user message, re-surfacing the checklist into the running conversation.</item>
/// </list>
/// Stateless config, per-pass counters — one instance is built per <c>Create</c> call, so
/// its counters are naturally scoped to the single pass it governs.
/// </summary>
public sealed class MasterLoopGovernorChatClient(IChatClient inner, MasterLoopHooks hooks)
    : DelegatingChatClient(inner)
{
    private int _iterations;
    private int _editlessStreak;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _iterations++;

        // The within-pass money fence — checked BEFORE spending another iteration.
        if (hooks.IsBudgetExhausted?.Invoke() == true)
            throw new MasterBudgetExhaustedException(
                "The per-pipeline cost budget was exhausted mid-pass — the coding loop was "
                + "stopped on money (not the anti-runaway iteration ceiling). Partial work is "
                + "preserved and the run is recorded as cost-cap-exhausted.");

        var list = messages as IList<ChatMessage> ?? messages.ToList();
        UpdateDrift(list);

        if (ShouldInjectReminder())
        {
            var reminder = hooks.RenderReminder?.Invoke();
            if (!string.IsNullOrWhiteSpace(reminder))
            {
                list = new List<ChatMessage>(list) { new(ChatRole.User, reminder) };
                _editlessStreak = 0; // the nag resets the drift window
            }
        }

        var response = await base.GetResponseAsync(list, options, cancellationToken);
        hooks.RecordIterationUsage?.Invoke(response);
        return response;
    }

    // Periodic (every N) OR drift (K edit-less iterations). N<=0 disables the periodic
    // trigger; K<=0 disables the drift trigger.
    private bool ShouldInjectReminder()
    {
        var periodic = hooks.ReminderEveryNIterations > 0
            && _iterations % hooks.ReminderEveryNIterations == 0;
        var drift = hooks.DriftEditlessIterations > 0
            && _editlessStreak >= hooks.DriftEditlessIterations;
        return periodic || drift;
    }

    // Drift = the model reads/searches but does not edit. Reset the streak whenever the
    // latest assistant turn called an edit/write tool; otherwise grow it.
    private void UpdateDrift(IList<ChatMessage> messages)
    {
        var lastAssistant = messages.LastOrDefault(m => m.Role == ChatRole.Assistant);
        if (lastAssistant is null) return;
        var calls = lastAssistant.Contents.OfType<FunctionCallContent>().ToList();
        if (calls.Count == 0) return; // a text-only turn is not a drift signal by itself
        if (calls.Any(c => IsEditTool(c.Name)))
            _editlessStreak = 0;
        else
            _editlessStreak++;
    }

    private static bool IsEditTool(string? name) =>
        name is not null
        && (name.Contains("edit", StringComparison.OrdinalIgnoreCase)
            || name.Contains("write", StringComparison.OrdinalIgnoreCase));
}
