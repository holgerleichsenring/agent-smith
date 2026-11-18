using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-18-78efa: applying the deletion of a stored secret value (kind secret-values) needs secrets.write besides
/// admin.assistant — a deliberate exception to "the assistant's drafts need only admin.assistant": secret rights stay
/// with secrets. Apply refuses with this line before a run starts, and the config run refuses with it again.
/// </summary>
public static class SecretValuesApproval
{
    public const string Permission = "secrets.write";

    /// <summary>The refusal naming the permission and the items, or null when nothing in the draft needs it.</summary>
    public static string? Refusal(IReadOnlyList<ConfigChangeItem> changes, bool mayWriteSecrets)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (mayWriteSecrets) return null;
        var items = changes.Where(c => OrphanedSecretValues.Is(c.Kind)).Select(c => $"{c.Operation} {c.Kind}/{c.Id}").ToList();
        return items.Count == 0 ? null
            : $"Deleting a stored secret value ({string.Join(", ", items)}) needs the {Permission} permission besides "
              + $"admin.assistant, and whoever applies it here does not hold {Permission}. Nothing was applied: someone who "
              + $"holds {Permission} applies this draft, or the assistant drafts the rest without it.";
    }
}
