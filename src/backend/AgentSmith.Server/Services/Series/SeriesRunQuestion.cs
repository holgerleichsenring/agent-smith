using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: the master's question a series still waits on a person for; null when none waits.</summary>
internal static class SeriesRunQuestion
{
    internal static string? Of(SeriesRunRecord record) => record.Question is { Answer: null } waiting ? waiting.Text : null;
}
