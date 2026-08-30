using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-18e3: where the master's station claims accumulate while the loop runs.
/// <para>
/// It lives ON the pipeline rather than in the tool host because the master and every
/// sub-agent it fans out to get their own host instance over the same run — a worker that
/// mapped one entry group has to contribute to the same map. Restating a station replaces
/// the earlier claim, so a master that corrects itself does not leave two answers behind.
/// </para>
/// </summary>
public sealed class StationClaimLog
{
    private readonly object _gate = new();
    private readonly List<StationClaim> _claims = [];

    public static StationClaimLog GetOrCreate(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<StationClaimLog>(ContextKeys.StationClaims, out var existing)
            && existing is not null)
            return existing;
        var log = new StationClaimLog();
        pipeline.Set(ContextKeys.StationClaims, log);
        return log;
    }

    /// <summary>What this run claimed, or nothing when no map was ever stated.</summary>
    public static IReadOnlyList<StationClaim> In(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<StationClaimLog>(ContextKeys.StationClaims, out var log)
            && log is not null ? log.Claims : [];
    }

    public IReadOnlyList<StationClaim> Claims
    {
        get { lock (_gate) return [.. _claims]; }
    }

    public void Record(StationClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        lock (_gate)
        {
            _claims.RemoveAll(c => c.Station == claim.Station
                && string.Equals(c.Group, claim.Group, StringComparison.OrdinalIgnoreCase));
            _claims.Add(claim);
        }
    }
}
