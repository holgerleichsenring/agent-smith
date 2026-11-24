namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: one pass of the base-merge master over the sandbox's merge. 2026-11-24-9dc0a: and the
/// same conversation continued.</summary>
public interface IBaseMergeMaster
{
    Task<BaseMergePass> RunAsync(BaseMergeSituation situation, CancellationToken ct);

    /// <summary>Continues <paramref name="earlier"/>'s conversation with <paramref name="note"/>: with the file tools, or
    /// with conclude as the only tool and no check (<paramref name="concludeOnly"/>), for the reason a person reads.</summary>
    Task<BaseMergePass> ContinueAsync(BaseMergePass earlier, string note, bool concludeOnly, CancellationToken ct);
}
