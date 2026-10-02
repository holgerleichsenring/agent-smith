using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: one design conversation's claim on one repository's sandbox, across
/// the turns of that conversation.
/// <para>
/// WHAT IS HELD IS THE SANDBOX, NOT THE SCOPE. <see cref="SourceScopeSandbox"/> returns its
/// cached inner sandbox before it reports progress and before it materialises, and its
/// disposal disposes the gate that guards materialisation — so a reused scope object could
/// neither be refreshed nor re-materialised, and would report the previous turn's sha as
/// this turn's. Every turn therefore builds a FRESH scope and this class hands it the inner
/// sandbox the last one left behind.
/// </para>
/// </summary>
/// <para>
/// 2026-10-01-283dc: the hold no longer knows what it holds. A repository and an uploaded
/// website are both "spawn once per conversation, prepare every turn", so the caller hands in
/// the two steps and <paramref name="name"/>/<paramref name="revision"/> only key the claim.
/// </para>
public sealed class SourceScopeHold(
    IHeldSandboxRegister register,
    string conversationId,
    string name,
    string? revision,
    ILogger logger)
{
    private readonly string _key = HeldSandbox.KeyFor(conversationId, name, revision);

    /// <summary>
    /// The prepared sandbox and what <paramref name="prepare"/> says it is on — from the hold
    /// when this conversation left one that is still alive, and from <paramref name="spawn"/>
    /// otherwise. A sandbox that could not be prepared is removed here — a spawned one disposed,
    /// a held one force-removed — so a caller that sees the exception holds nothing.
    /// </summary>
    public async Task<(ISandbox Sandbox, string Sha)> OpenAsync(
        Func<string, CancellationToken, Task<ISandbox>> spawn,
        Func<ISandbox, CancellationToken, Task<string>> prepare, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(prepare);
        var held = await register.TakeAsync(_key, ct);
        if (held is null) return await SpawnedAsync(spawn, prepare, ct);
        logger.LogInformation(
            "Conversation {Conversation} reads '{Name}' through the sandbox it already holds",
            conversationId, name);
        try
        {
            return (held, await prepare(held, ct));
        }
        catch
        {
            await ForceRemoveAsync(held);
            throw;
        }
    }

    private async Task<(ISandbox Sandbox, string Sha)> SpawnedAsync(
        Func<string, CancellationToken, Task<ISandbox>> spawn,
        Func<ISandbox, CancellationToken, Task<string>> prepare, CancellationToken ct)
    {
        var created = await spawn(conversationId, ct);
        try
        {
            return (created, await prepare(created, ct));
        }
        catch
        {
            await created.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Hands the turn's sandbox back to the register for the next turn. False when this
    /// backend cannot force-remove one — the caller then disposes it, as it always did,
    /// because a hold whose release would wait out a shutdown grace is a hold that would
    /// make a capacity door wait for it.
    /// </summary>
    public bool Keep(ISandbox inner)
    {
        if (inner is not IHoldableSandbox holdable) return false;
        register.Hold(new HeldSandbox(_key, conversationId, holdable));
        return true;
    }

    private async Task ForceRemoveAsync(IHoldableSandbox held)
    {
        try
        {
            await held.ForceRemoveAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Could not remove the held sandbox of '{Name}' after it failed to prepare", name);
        }
    }
}
