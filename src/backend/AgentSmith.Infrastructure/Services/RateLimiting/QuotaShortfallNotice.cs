using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-ab45: what one call to <paramref name="deployment"/> knows about not fitting a minute's quota. Once it is
/// known — the reservation is larger than the budget, the provider counted more than it, or the provider asks for
/// longer than the 120 s a wait is capped at — it is said once on the run and in the log
/// (<see cref="ModelQuotaWaitLine.CannotFitAsync"/>), and every later waiting line of the call adds it to its reason.
/// The call keeps waiting and retrying as before.
/// </summary>
internal sealed class QuotaShortfallNotice(string deployment, ModelQuotaWaitLine? waitLine, ILogger logger)
{
    private const string Key = Contracts.Models.Configuration.AgentConfig.RateLimitKey + ".input_tokens_per_minute";

    /// <summary>Why the call cannot fit, null while nothing says so.</summary>
    public string? Reason { get; private set; }

    /// <summary><paramref name="reason"/> with what this call cannot fit, when it is known.</summary>
    public string With(string reason) => Reason is null ? reason : $"{reason}; {Reason}";

    public Task TooLargeAsync(QuotaShortfall shortfall, CancellationToken cancellationToken)
    {
        var reason = $"it needs ~{shortfall.Needed} tokens{Parts(shortfall)}, more than {Key} {shortfall.Budget} (the provider default when unset)";
        return SayAsync(reason, $"A call to {deployment} cannot fit a minute's quota: {reason}. It keeps waiting and retrying.", cancellationToken);
    }

    /// <summary>The provider's own count was more than the whole minute: this call passed, calls its size wait for a full bucket.</summary>
    public Task UsedTooMuchAsync(QuotaShortfall shortfall, CancellationToken cancellationToken)
    {
        var reason = $"a call used ~{shortfall.Needed} tokens{Parts(shortfall)}, more than {Key} {shortfall.Budget}";
        return SayAsync(reason, $"A call to {deployment} counted more than a minute's quota: {reason}. Calls this size wait for a full minute each.", cancellationToken);
    }

    public Task TooLongAsync(TimeSpan asked, string? providerWords, CancellationToken cancellationToken)
    {
        var reason = $"the provider asks to wait {(long)asked.TotalSeconds} s, longer than a minute's quota"
            + (providerWords is null ? "" : $" (it says: \"{providerWords}\")");
        return SayAsync(reason, $"A call to {deployment} cannot fit a minute's quota: {reason}. It keeps waiting and retrying.", cancellationToken);
    }

    // The output part only where the provider counts it, named by what it is (2026-11-24-9dc0d).
    private static string Parts(QuotaShortfall s) =>
        s.Output > 0 ? $" (input ~{s.Input} + {(s.IsGenerated ? "generated" : "max_tokens")} {s.Output})" : "";

    private Task SayAsync(string reason, string sentence, CancellationToken cancellationToken)
    {
        if (Reason is not null) return Task.CompletedTask;
        Reason = reason;
        if (waitLine is not null) return waitLine.CannotFitAsync(deployment, sentence, cancellationToken);
        logger.LogWarning("{CannotFit}", sentence);
        return Task.CompletedTask;
    }
}
