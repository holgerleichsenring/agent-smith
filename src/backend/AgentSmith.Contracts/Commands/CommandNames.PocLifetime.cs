namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-23-c81ee: the steps of a PoC's lifetime runs.</summary>
public static partial class CommandNames
{
    /// <summary>Extends a PoC by pocs.lifetime.extend_days — an owner up to pocs.lifetime.max_owner_extensions times, a
    /// pocs.admin holder past them — and records the extension.</summary>
    public const string PocExtend = "PocExtendCommand";

    /// <summary>Deletes the stored PoC uploads older than pocs.blocked_retention_days and what deleted PoCs still hold of
    /// their variables.</summary>
    public const string PurgePocUploads = "PurgePocUploadsCommand";
}
