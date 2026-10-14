using System.Globalization;
using AgentSmith.Server.Services.Tickets.Runway;
using static AgentSmith.Server.Services.Tickets.Runway.SeriesHeldReasons;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-8762v: what agent-smith says once when it holds a ticket's series or its start was refused — the
/// reason, spend against cap, run time, the specs left and how to go on. No Retry is named: Retry refuses every
/// end a spoken hold follows.
/// </summary>
public static class SeriesTexts
{
    public const string HowToGoOn = "A comment on the ticket or an approval in its conversation starts the next attempt.";

    public static string Held(string code, SeriesRunwayRead read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var reason = code switch
        {
            NoProgress => "the last attempt finished no spec",
            BudgetUsd => "the chain of attempts has spent the series budget (series.budget_usd)",
            WallClock => "the chain of attempts has used the series run time (series.wall_clock_hours)",
            DefaultPending => "a default marked holds_series waits for a person — ratify it on the ticket page",
            Misconfigured => read.Settings.Refusal() ?? "the series settings are invalid",
            _ => code,
        };
        return $"Agent Smith — the next attempt of this ticket's spec series is held: {reason}. {Standing(read)} {HowToGoOn}";
    }

    public static string Refused(string reason, SeriesRunwayRead read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return $"Agent Smith — the next attempt of this ticket's spec series could not be started: {reason}. {Standing(read)} {HowToGoOn}";
    }

    private static string Standing(SeriesRunwayRead read)
    {
        var view = read.View;
        return string.Create(CultureInfo.InvariantCulture,
            $"Spent ${view.SpentUsd:0.00} of ${view.CapUsd:0.00} and {view.ElapsedHours:0.0} of {view.CapHours:0.#} run hours; {read.SpecsLeft} spec(s) left.");
    }
}
