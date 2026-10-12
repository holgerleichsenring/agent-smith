using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Json;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: the two texts a parked run is resumed from — the context entries
/// <c>[{K,T,V}]</c> and the remaining commands — wherever they are stored: the RunCheckpoint row,
/// the RunCheckpointed trail payload and the resume payload a queued relaunch carries.
/// <para>
/// K and T go through <see cref="CheckpointRenames"/>, the table the restoring reader consults; V
/// is walked as the type T names (resolved the way the reader resolves it), so a member that
/// names an execution stage inside a context value is never touched. The command shape mirrors
/// CheckpointCommand (Application): its accepted spec-id alias and the names
/// <see cref="CommandNames.Declared"/> maps.
/// </para>
/// </summary>
public sealed class CheckpointJsonRewriter(SpecRenameJson json, SpecApprovalJsonRewriter approvals)
{
    /// <summary>The rewritten context entries, or null when nothing moved.</summary>
    public string? RewriteContext(string? contextJson) =>
        json.Rewrite(contextJson, node => node is JsonArray entries && RewriteEntries(entries));

    /// <summary>The rewritten command list, or null when nothing moved.</summary>
    public string? RewriteCommandsJson(string? commandsJson) =>
        json.Rewrite(commandsJson, node => node is JsonArray commands && RewriteCommands(commands));

    /// <summary>Renames each command's spec member and maps an old command name to the declared one.</summary>
    public bool RewriteCommands(JsonArray commands)
    {
        var changed = false;
        foreach (var command in commands.OfType<JsonObject>())
        {
            changed |= json.Move(command, "PhaseId", "SpecId");
            if (command["Name"] is JsonValue name && name.TryGetValue<string>(out var stored)
                && CommandNames.Declared(stored) is var declared && declared != stored)
            {
                command["Name"] = declared;
                changed = true;
            }
        }
        return changed;
    }

    private bool RewriteEntries(JsonArray entries)
    {
        var changed = false;
        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (entry["K"]?.GetValue<string>() is not { } key || entry["T"]?.GetValue<string>() is not { } typeName) continue;
            var (newKey, newType) = CheckpointRenames.Rewrite(key, typeName);
            if (newKey is not null) entry["K"] = newKey;
            if (newType is not null) entry["T"] = newType;
            changed |= newKey is not null || newType is not null;
            changed |= json.RewriteNested(entry, "V", value => RewriteValue(newKey ?? key, newType ?? typeName, value));
        }
        return changed;
    }

    // A value is walked as its own type; the approved set rides as a string holding its own JSON.
    private string? RewriteValue(string key, string typeName, string value)
    {
        if (CheckpointRenames.Resolve(typeName) is not { } type) return null;
        if (type != typeof(string)) return json.Rewrite(value, type, policy: null);
        if (key != ContextKeys.ApprovedSpecSet) return null;
        var record = JsonSerializer.Deserialize<string>(value);
        return approvals.Rewrite(record) is { } rewritten ? JsonSerializer.Serialize(rewritten) : null;
    }
}
