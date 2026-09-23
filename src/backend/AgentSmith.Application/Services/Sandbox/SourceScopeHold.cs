using AgentSmith.Contracts.Models.Configuration;
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
public sealed class SourceScopeHold(
    IHeldSandboxRegister register,
    string conversationId,
    RepoConnection repo,
    string? revision,
    ILogger logger)
{
    private readonly string _key = HeldSandbox.KeyFor(conversationId, repo.Name, revision);

    /// <summary>
    /// The prepared sandbox and the sha it is on — from the hold when this conversation left
    /// one that is still alive, and from a spawn otherwise. A held sandbox that could not be
    /// prepared is force-removed here: it is out of the register and nobody else can reach it.
    /// </summary>
    public async Task<(ISandbox Sandbox, string Sha)> OpenAsync(
        ResolvedProject project, SourceScopeOpener opener, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(opener);
        var held = await register.TakeAsync(_key, ct);
        if (held is null) return await opener.OpenAsync(project, repo, revision, ct, conversationId);
        logger.LogInformation(
            "Conversation {Conversation} reads '{Repo}' through the sandbox it already holds",
            conversationId, repo.Name);
        try
        {
            return (held, await opener.PrepareAsync(held, repo, revision, ct));
        }
        catch
        {
            await ForceRemoveAsync(held);
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
                "Could not remove the held sandbox of '{Repo}' after it failed to prepare", repo.Name);
        }
    }
}
