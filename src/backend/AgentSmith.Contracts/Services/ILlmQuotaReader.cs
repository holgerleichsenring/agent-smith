namespace AgentSmith.Contracts.Services;

/// <summary>2026-11-22-66e4a: reads the quota every deployment has left in this process.</summary>
public interface ILlmQuotaReader
{
    IReadOnlyList<LlmQuotaSnapshot> Read();
}
