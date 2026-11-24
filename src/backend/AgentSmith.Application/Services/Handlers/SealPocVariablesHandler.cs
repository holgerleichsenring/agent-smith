using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Sealing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-933dg: seals a PoC's secret variables and, with pull_mode pull_secret, its pull secret to the target's
/// certificate and leaves the files, the fingerprint and the variables checksum under ContextKeys.PocSeal for the GitOps
/// commit (2026-11-23-c81eb), which alone records the fingerprint once its commit has landed. Every refusal comes before
/// anything is sealed. Names only in what it logs or says; until the commit step ships nothing is committed.
/// </summary>
public sealed class SealPocVariablesHandler(
    IPocVariableStore store, PocsConfigSource config, PocSealPlanner planner, PocSealer sealer,
    ILogger<SealPocVariablesHandler> logger) : ICommandHandler<SealPocVariablesContext>
{
    public async Task<CommandResult> ExecuteAsync(SealPocVariablesContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Pipeline.TryGet<string>(ContextKeys.PocActSettled, out var settled) && !string.IsNullOrEmpty(settled))
            return CommandResult.Ok($"Nothing to seal: {settled}."); // 2026-11-23-c81ee
        if (context.Seeds.PocId is not { } pocId || await store.ReadAsync(pocId, cancellationToken) is not { } poc)
            return CommandResult.Fail("A PoC run's seal needs the PoC it was seeded with; none was found.");
        if (config.Current().Targets.FirstOrDefault(t => string.Equals(t.Name, poc.Target, StringComparison.OrdinalIgnoreCase)) is not { } target)
            return CommandResult.Fail($"'{poc.Name}' names the target '{poc.Target}', which pocs.targets no longer holds.");
        var (plan, refused) = await planner.PlanAsync(poc, target, cancellationToken);
        if (plan is null) return CommandResult.Fail(refused!);
        var (result, refusal) = sealer.Seal(plan, poc, target);
        if (result is null) return CommandResult.Fail(refusal!);
        context.Pipeline.Set(ContextKeys.PocSeal, result);
        var names = plan.Secrets.Keys.Order(StringComparer.Ordinal).Concat(plan.PullSecret is null ? [] : [PocSecretNames.Pull]).ToList();
        logger.LogInformation("PoC {PocId} sealed: {Names} (unchanged {Unchanged})", poc.PocId, string.Join(", ", names), result.Unchanged);
        return CommandResult.Ok(result.Unchanged
            ? $"{names.Count} name(s) unchanged since the last commit; nothing is committed before the GitOps commit step."
            : $"sealed {names.Count} name(s) ({string.Join(", ", names)}); nothing is committed before the GitOps commit step.");
    }
}
