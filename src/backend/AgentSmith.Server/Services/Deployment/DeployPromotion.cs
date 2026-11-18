using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Deployment;

/// <summary>
/// 2026-11-18-3863h: promotion by the template's rule. A deploy that settled green deploys each environment the run said
/// promotes from it without approval, through <see cref="DeployLauncher"/> with the settled version, listed on the source
/// run's ticket. The record is settled in the store first and only by one caller, so a promotion starts once; the live
/// binding is read again, and an environment whose approval was turned on since is left for a person. What started, or
/// why not, is the record's note.
/// </summary>
public sealed class DeployPromotion(DeployLauncher launcher, DeployRecordRepository records, ILogger<DeployPromotion> logger)
{
    public async Task PromoteAsync(DeployRecord record, ResolvedProject project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(project);
        if (!DeployFollowVerdict.Succeeded(record) || string.IsNullOrEmpty(record.PromotesTo)) return;
        if (!await records.SettleOnceAsync(record, ct)) return;
        var ticket = await records.TicketOfRunAsync(record.RunId, ct);
        var said = new List<string>();
        foreach (var environment in record.PromotesTo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            said.Add(await OneAsync(record, project, environment, ticket, ct));
        record.Note = string.Join("; ", new[] { record.Note }.Concat(said).Where(s => !string.IsNullOrEmpty(s)));
    }

    private async Task<string> OneAsync(DeployRecord record, ResolvedProject project, string environment, string? ticket, CancellationToken ct)
    {
        var (target, error) = DeploymentTarget.Of(project, new DeployRequest(record.Context, record.ContextRepo, environment, null));
        if (target is null) return $"'{environment}' not promoted: {error}";
        if (target.Environment.Approval)
            return $"'{environment}' not promoted: its approval is on now, so a person deploys it";
        var launch = await launcher.PromoteAsync(project.Name,
            new DeployLaunchRequest(record.Context, environment, null, record.ContextRepo, ticket), record.Version, ct);
        logger.LogInformation("Deploy {RunId} settled green; promoting {Context} {Version} to {Environment}: {Outcome}",
            record.RunId, record.Context, record.Version, environment, launch.Outcome);
        return launch.Outcome == DeployLaunchOutcome.Started
            ? $"promoted {record.Version} to '{environment}' (run {launch.RunId})"
            : $"'{environment}' not promoted: {launch.Reason}";
    }
}
