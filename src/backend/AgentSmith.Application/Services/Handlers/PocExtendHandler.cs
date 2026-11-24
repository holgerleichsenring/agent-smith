using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-c81ee: the one step of a poc-extend run — extends a PoC by pocs.lifetime.extend_days from the later of now
/// and its expiry, counting the extension. An owner extends while the count is below pocs.lifetime.max_owner_extensions;
/// past it only a run whose route found the caller holding pocs.admin (ContextKeys.PocMayExceed). A deleted PoC is
/// refused. The extension is audited 'extended' from its PocActedEvent; it starts nothing — Start is c81ed's act.
/// </summary>
public sealed class PocExtendHandler(
    IPocLifetimeStore store, PocsConfigSource config, PocLifetime lifetime, PocActedPublisher publisher, TimeProvider clock,
    ILogger<PocExtendHandler> logger) : ICommandHandler<PocExtendContext>
{
    public async Task<CommandResult> ExecuteAsync(PocExtendContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Seeds.PocId is not { } pocId || await store.ReadAsync(pocId, cancellationToken) is not { } poc)
            return CommandResult.Fail("An extend run needs the PoC it was seeded with; none was found.");
        if (!PocStates.IsLive(poc.State)) return CommandResult.Fail($"'{poc.Name}' is {poc.State}; there is nothing to extend.");
        var pocs = config.Current();
        if (lifetime.TargetOf(pocs, poc.Target) is not { } target)
            return CommandResult.Fail($"'{poc.Name}' names the target '{poc.Target}', which pocs.targets no longer holds.");
        var allowance = pocs.Lifetime.MaxOwnerExtensions;
        if (poc.Lifetime.Extensions >= allowance && !context.Seeds.MayExceed)
            return CommandResult.Fail($"'{poc.Name}' was extended {poc.Lifetime.Extensions} time(s); an owner extends a PoC at most "
                + $"{allowance} times ({PocLifetimeConfig.MaxOwnerExtensionsKey}), because a PoC is not a product. A pocs.admin holder may extend it further.");
        var now = clock.GetUtcNow();
        var current = lifetime.Of(poc.Lifetime, target, now).ExpiresAt;
        var expires = (current > now ? current : now).AddDays(pocs.Lifetime.ExtendDays);
        await store.ExtendAsync(pocId, expires.UtcTicks, cancellationToken);
        var count = poc.Lifetime.Extensions + 1;
        var reason = $"extension {count}{(count > allowance ? $", past {PocLifetimeConfig.MaxOwnerExtensionsKey} ({allowance}) by an admin" : $" of {allowance}")}: "
            + $"expires on {expires:yyyy-MM-dd} (+{pocs.Lifetime.ExtendDays} days, {PocLifetimeConfig.ExtendDaysKey})";
        await publisher.PublishExtendedAsync(context.Pipeline, pocId, context.Seeds.Actor, reason, cancellationToken);
        logger.LogInformation("PoC {PocId} extended to {Expires} (extension {Count})", pocId, expires, count);
        return CommandResult.Ok($"'{poc.Name}' {reason}.");
    }
}
