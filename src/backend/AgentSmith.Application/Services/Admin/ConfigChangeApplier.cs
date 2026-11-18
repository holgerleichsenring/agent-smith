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
public sealed class ConfigChangeApplier(IConfigStore store, ConfigEntityKinds kinds, ILogger<ConfigChangeApplier> logger)
{
    public ConfigApplyReport Apply(ConfigChangeRequest request, string runId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var by = new ChangeAttribution($"{request.Person} via admin assistant (run {runId})");
        for (var i = 0; i < request.Changes.Count; i++)
        {
            var item = request.Changes[i];
            try
            {
                ApplyOne(item, by);
            }
            catch (Exception ex) when (ex is ConfigurationException or StaleConfigVersionException or System.Text.Json.JsonException)
            {
                logger.LogInformation(ex, "Config run {RunId} refused change {Index}: {Kind}/{Id}", runId, i + 1, item.Kind, item.Id);
                return new ConfigApplyReport(false, $"Change {i + 1} ({item.Operation} {item.Kind}/{item.Id}) was refused: "
                    + $"{ex.Message} {Revert(by)}");
            }
        }
        return new ConfigApplyReport(true, $"Applied {request.Changes.Count} configuration change(s) in run {runId}, as {by.Actor}.");
    }

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
