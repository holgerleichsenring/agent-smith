namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-12-f5f7b: what one run repository answered for a ticket's sub-series.</summary>
public enum SubSeriesState
{
    /// <summary>The repository has no sandbox in this run: its path was never looked at.</summary>
    NotCheckedOut = 0,

    /// <summary>No manifest in the repository names the ticket.</summary>
    Nothing = 1,

    /// <summary>Something is there and did not read back.</summary>
    Unreadable = 2,

    /// <summary>The sub-series read back.</summary>
    Found = 3,
}
