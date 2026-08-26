using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// p0510: the one copy of the config studio's write ceremony. Every studio surface
/// — entity CRUD, import, revert, settings — takes the same attribution,
/// translates the same two validation exceptions into 409/400, and emits the same
/// post-commit reload signal. Split out of ConfigStudioEndpoints so the per-surface
/// route files share it instead of each carrying a copy.
/// </summary>
internal static class ConfigStudioWriteGuard
{
    /// <summary>
    /// p0503d: the audit trail names the PRINCIPAL, or nobody. This used to read an
    /// <c>X-Actor</c> request header — a client-supplied string, written verbatim into the
    /// change record — and the dashboard never sent it, so every installation already got
    /// the default and the header was pure forgery surface. Deleting the read closes it for
    /// every caller at once rather than only for authenticated ones, and changes nothing
    /// for anyone. <c>Identity.Name</c> is the claim the auth block configures.
    /// </summary>
    internal static ChangeAttribution Attribution(HttpContext ctx) =>
        new(ctx.User.Identity is { IsAuthenticated: true, Name: { Length: > 0 } name }
            ? name
            : "dashboard");

    // p0353: run a config WRITE, and on success bump the config epoch + publish a
    // ConfigChangedEvent so the poller leader and settings enforcers pick the change
    // up live (no restart). The signal is best-effort and post-commit — a signal
    // failure must never fail an already-durable write, and the known validation
    // exceptions short-circuit BEFORE signalling (no epoch bump on a rejected write).
    internal static Task<IResult> GuardSignalingAsync(
        HttpContext ctx, IConfigReloadSignal reload, ISystemEventPublisher events, Func<IResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return GuardSignalingAsync(ctx, reload, events, () => Task.FromResult(action()));
    }

    /// <summary>
    /// 2026-08-26-7a51: the same ceremony for a write that has to await — the access
    /// surface reads its own answer back, and a removal that trips the admin invariant has
    /// to arrive as the 400 an edited form's would.
    /// </summary>
    internal static async Task<IResult> GuardSignalingAsync(
        HttpContext ctx, IConfigReloadSignal reload, ISystemEventPublisher events, Func<Task<IResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        IResult result;
        try
        {
            result = await action();
        }
        catch (StaleConfigVersionException ex)
        {
            // p0349: a concurrent edit moved the entity's version on — 409, never a
            // silent last-write-wins. The client reloads and retries.
            return Results.Conflict(new { error = ex.Message });
        }
        catch (ConfigurationException ex)
        {
            // Referential integrity / validation failure — a client error, not a 500.
            return Results.BadRequest(new { error = ex.Message });
        }

        await SignalConfigChangedAsync(reload, events, Attribution(ctx).Actor);
        return result;
    }

    private static async Task SignalConfigChangedAsync(
        IConfigReloadSignal reload, ISystemEventPublisher events, string actor)
    {
        // CancellationToken.None: the write already committed, so the reload signal
        // must fire even if the client disconnected — otherwise the leader stays stale.
        try
        {
            var epoch = await reload.BumpAsync(CancellationToken.None);
            await events.PublishAsync(
                new ConfigChangedEvent("config-studio", epoch, actor, DateTimeOffset.UtcNow), CancellationToken.None);
        }
        catch
        {
            // Best-effort: a bump/publish failure is swallowed so the write still returns 2xx.
        }
    }
}
