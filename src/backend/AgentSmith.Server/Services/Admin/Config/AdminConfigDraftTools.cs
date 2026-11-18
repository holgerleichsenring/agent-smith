using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Services.Admin;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Admin.Config;

/// <summary>
/// 2026-11-17-f3a8c: the admin assistant's configuration draft — read an entity as the store holds it, draft its
/// full new document or its removal, drop a drafted change. Nothing is written here: the draft is shown with an Apply
/// button, and Apply runs a config run.
/// </summary>
public sealed class AdminConfigDraftTools(IConfigStore store, ConfigEntityKinds kinds, AdminConfigDraft draft, ILogger logger)
{
    [Description("Reads one configuration entity as the Config studio holds it (null when absent). kind is one of: "
        + "agents, trackers, repos, projects, mcp-servers, secrets, connections, design-sources, settings/<type>; for "
        + "settings the id is ignored.")]
    public string ReadConfigEntity(string kind, string id)
    {
        try
        {
            return kinds.Read(store, kind, id) ?? "null";
        }
        catch (ConfigurationException ex)
        {
            logger.LogDebug(ex, "The admin assistant read an unknown configuration kind {Kind}", kind);
            return ex.Message;
        }
    }

    [Description("Drafts a configuration change for the administrator to apply: operation 'upsert' with the entity's "
        + "full JSON document (as read_config_entity shows it; to duplicate, read one and draft it under the new id), or "
        + "'delete'. Drafting the same kind and id again replaces that change. Returns the draft so far.")]
    public string DraftConfigChange(string operation, string kind, string id, string? document = null)
    {
        try
        {
            draft.Put(Item(operation, kind, id, document));
            return Summary();
        }
        catch (Exception ex) when (ex is ConfigurationException or JsonException)
        {
            logger.LogDebug(ex, "The admin assistant's draft of {Kind}/{Id} was not taken", kind, id);
            return $"Not drafted: {ex.Message}";
        }
    }

    [Description("Removes the drafted change of one entity from the draft. Returns the draft so far.")]
    public string DropConfigChange(string kind, string id) =>
        draft.Drop(kind, id) ? Summary() : $"No change of {kind}/{id} is drafted. {Summary()}";

    private ConfigChangeItem Item(string operation, string kind, string id, string? document)
    {
        var before = kinds.Read(store, kind, id);
        if (operation == ConfigChangeItem.Delete) return new ConfigChangeItem(operation, kind, id, null, before);
        if (operation != ConfigChangeItem.Upsert) throw new ConfigurationException($"operation is 'upsert' or 'delete', not '{operation}'.");
        using var parsed = JsonDocument.Parse(document ?? throw new ConfigurationException("an upsert carries the entity's document."));
        return new ConfigChangeItem(operation, kind, id, parsed.RootElement.GetRawText(), before);
    }

    private string Summary() => draft.Items.Count == 0
        ? "The draft is empty."
        : "Drafted: " + string.Join("; ", draft.Items.Select(i => $"{i.Operation} {i.Kind}/{i.Id}"));
}
