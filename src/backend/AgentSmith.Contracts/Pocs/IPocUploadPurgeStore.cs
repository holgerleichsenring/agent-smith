namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81ee: what the daily poc-upload-purge run deletes — stored PoC uploads older than
/// pocs.blocked_retention_days, and what a deleted PoC still holds of its variables.</summary>
public interface IPocUploadPurgeStore
{
    /// <summary>Deletes every stored PoC upload file created before <paramref name="olderThan"/>; how many.</summary>
    Task<int> PurgeUploadsAsync(DateTimeOffset olderThan, CancellationToken ct);

    /// <summary>The deleted PoCs that still hold stored secret values or plain variables.</summary>
    Task<IReadOnlyList<long>> DeletedWithVariablesAsync(CancellationToken ct);

    /// <summary>Clears a deleted PoC's plain variables.</summary>
    Task ClearPlainVariablesAsync(long pocId, CancellationToken ct);
}
