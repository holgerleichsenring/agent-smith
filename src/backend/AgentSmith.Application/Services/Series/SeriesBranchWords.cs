using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-24-f9c5b: what the series' own texts call the branch its check reads — its series branch, or main for a
/// series without one.</summary>
public static class SeriesBranchWords
{
    public static string Checked(SeriesRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.IntegrationBranch ?? "main";
    }
}
