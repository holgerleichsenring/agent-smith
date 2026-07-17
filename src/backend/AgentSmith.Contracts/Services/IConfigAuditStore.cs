using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Append-only writer + reader for the versioned config audit trail. Every
/// mutation on <c>IConfigStore</c> appends one attributed row; the store both
/// reads the feed for the Changes view and marks a row reverted when its inverse
/// is replayed.
/// </summary>
public interface IConfigAuditStore
{
    /// <summary>Append a change and return the row with its assigned version + id.</summary>
    ConfigChange Append(
        string actor,
        ConfigEntityType entityType,
        string entityId,
        ConfigChangeOperation operation,
        string? beforeJson,
        string? afterJson);

    /// <summary>All changes, newest first.</summary>
    IReadOnlyList<ConfigChange> GetAll();

    ConfigChange? GetById(string id);

    /// <summary>Flag a prior row as reverted (its inverse has been replayed).</summary>
    void MarkReverted(string id);
}
