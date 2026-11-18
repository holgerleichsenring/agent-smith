using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8c: applies approved configuration changes in order through the Config studio's store, so its
/// validators decide. An item whose entity changed since it was drafted is refused; the first refusal stops the run
/// and the items this run applied before it are reverted, newest first. That is compensation, not a transaction: a
/// revert the store refuses is named in the report.
/// </summary>
public sealed class ConfigChangeApplier(
    IConfigStore store, ConfigEntityKinds kinds, OrphanedSecretValues orphans, ILogger<ConfigChangeApplier> logger)
{
    /// <summary>
    /// 2026-11-18-00eaa: catalog items first, in order; then the deletions of orphaned stored values, which cannot be
    /// reverted — so every one of them is checked before any is deleted, and nothing irreversible happens before the
    /// last check. A value set for the same name between the check and the delete is lost (accepted, narrow).
    /// 2026-11-18-78efa: a stored value is deleted only for a person holding secrets.write — checked before anything.
    /// </summary>
    public async Task<ConfigApplyReport> ApplyAsync(ConfigChangeRequest request, string runId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (SecretValuesApproval.Refusal(request.Changes, request.MayWriteSecrets) is { } refused) return new ConfigApplyReport(false, refused);
        var by = new ChangeAttribution($"{request.Person} via admin assistant (run {runId})");
        var numbered = request.Changes.Select((item, i) => (Item: item, Number: i + 1)).ToList();
        foreach (var (item, number) in numbered.Where(n => !OrphanedSecretValues.Is(n.Item.Kind)))
        {
            try
            {
                ApplyOne(item, by);
            }
            catch (Exception ex) when (ex is ConfigurationException or StaleConfigVersionException or System.Text.Json.JsonException)
            {
                logger.LogInformation(ex, "Config run {RunId} refused change {Index}: {Kind}/{Id}", runId, number, item.Kind, item.Id);
                return Refused(number, item, ex.Message, by);
            }
        }
        var values = numbered.Where(n => OrphanedSecretValues.Is(n.Item.Kind)).ToList();
        foreach (var (item, number) in values)
            if (item.Operation != ConfigChangeItem.Delete || item.Before is null || await orphans.ReadAsync(store, item.Id, ct) != item.Before)
                return Refused(number, item, "it is no longer the orphaned stored value the draft showed; ask the assistant to read it again.", by);
        foreach (var (item, _) in values) await orphans.DeleteAsync(item.Id, ct);
        return new ConfigApplyReport(true, $"Applied {request.Changes.Count} configuration change(s) in run {runId}, as {by.Actor}.");
    }

    private ConfigApplyReport Refused(int number, ConfigChangeItem item, string reason, ChangeAttribution by) =>
        new(false, $"Change {number} ({item.Operation} {item.Kind}/{item.Id}) was refused: {reason} {Revert(by)}");

    private void ApplyOne(ConfigChangeItem item, ChangeAttribution by)
    {
        if (kinds.Read(store, item.Kind, item.Id) != item.Before)
            throw new ConfigurationException("it changed since it was drafted; ask the assistant to read it again.");
        if (item.Operation == ConfigChangeItem.Delete) kinds.Delete(store, item.Kind, item.Id, by);
        else kinds.Upsert(store, item.Kind, item.Id, item.Document ?? "{}", by);
    }

    private string Revert(ChangeAttribution by)
    {
        var applied = store.GetChanges().Where(c => c.Actor == by.Actor && !c.Reverted).ToList();
        if (applied.Count == 0) return "Nothing had been applied.";
        var failed = new List<string>();
        foreach (var change in applied)
        {
            try
            {
                store.Revert(change.Id, by);
            }
            catch (Exception ex) when (ex is ConfigurationException or StaleConfigVersionException)
            {
                logger.LogWarning(ex, "Reverting change {ChangeId} of {EntityId} failed", change.Id, change.EntityId);
                failed.Add($"{change.EntityId}: {ex.Message}");
            }
        }
        return failed.Count == 0
            ? $"The {applied.Count} change(s) this run applied before it were reverted."
            : $"Reverting failed for {string.Join("; ", failed)}.";
    }
}
