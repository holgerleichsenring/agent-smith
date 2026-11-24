using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-c81ee: the one step of the daily poc-upload-purge run on the installation — deletes the stored PoC uploads
/// older than pocs.blocked_retention_days (a revision whose upload is gone can no longer be overruled, and a blocked PoC
/// that never deployed is removed by the next lifetime pass), and, for every deleted PoC, its stored secret values
/// through <see cref="SecretWrite"/> in the PoC's scope and its plain variables. It says how many of each; a run with
/// nothing left does nothing.
/// </summary>
public sealed class PurgePocUploadsHandler(
    IPocUploadPurgeStore store, ISecretVault vault, SecretWrite secrets, PocsConfigSource config, TimeProvider clock,
    ILogger<PurgePocUploadsHandler> logger) : ICommandHandler<PurgePocUploadsContext>
{
    public const string Actor = "poc-upload-purge";

    public async Task<CommandResult> ExecuteAsync(PurgePocUploadsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var days = config.Current().BlockedRetentionDays;
        var files = await store.PurgeUploadsAsync(clock.GetUtcNow().AddDays(-days), cancellationToken);
        var runId = context.Pipeline.TryGet<string>(ContextKeys.RunId, out var id) ? id ?? string.Empty : string.Empty;
        var (pocs, values, refused) = (0, 0, new List<string>());
        foreach (var pocId in await store.DeletedWithVariablesAsync(cancellationToken))
        {
            var (deleted, failed) = await DeleteSecretsAsync(pocId, runId, cancellationToken);
            values += deleted;
            refused.AddRange(failed);
            await store.ClearPlainVariablesAsync(pocId, cancellationToken);
            pocs++;
        }
        logger.LogInformation("PoC purge: {Files} upload file(s), {Values} secret value(s) of {Pocs} deleted PoC(s)", files, values, pocs);
        var said = $"{files} stored PoC upload file(s) older than {days} days ({PocsConfig.BlockedRetentionDaysKey}) deleted; "
            + $"{values} secret value(s) and the plain variables of {pocs} deleted PoC(s) removed.";
        return refused.Count == 0 ? CommandResult.Ok(said) : CommandResult.Fail($"{said} Not deleted: {string.Join(" ", refused)}");
    }

    private async Task<(int Deleted, IReadOnlyList<string> Failed)> DeleteSecretsAsync(long pocId, string runId, CancellationToken ct)
    {
        if (vault.Unavailable is not null) return (0, []);
        var (deleted, failed) = (0, new List<string>());
        var scope = SecretScope.Poc(pocId);
        foreach (var state in await vault.StatesAsync(scope, ct))
        {
            var report = await secrets.RunAsync(new SecretWriteRequest(state.Name, SecretWriteRequest.Delete, null, Actor, scope), runId, ct);
            if (report.Written) deleted++;
            else failed.Add(report.Line);
        }
        return (deleted, failed);
    }
}
