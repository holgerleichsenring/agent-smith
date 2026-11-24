namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: one pass of the base-merge master over the sandbox's merge.</summary>
public interface IBaseMergeMaster
{
    Task<BaseMergePass> RunAsync(BaseMergeSituation situation, CancellationToken ct);
}
