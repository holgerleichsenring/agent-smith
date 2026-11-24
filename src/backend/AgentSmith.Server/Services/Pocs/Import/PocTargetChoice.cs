using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: the target an upload or import names — the single configured one when none is named,
/// required when several are configured; an unknown name is refused naming the ones there are.</summary>
internal static class PocTargetChoice
{
    /// <summary>The target, or the sentence that refuses the request.</summary>
    public static (PocTargetConfig? Target, string? Refusal) Of(PocsConfig pocs, string? named)
    {
        ArgumentNullException.ThrowIfNull(pocs);
        var names = string.Join(", ", pocs.Targets.Select(t => t.Name));
        if (string.IsNullOrWhiteSpace(named))
            return pocs.Targets.Count == 1
                ? (pocs.Targets[0], null)
                : (null, $"Name a target: pocs.targets holds {pocs.Targets.Count} ({names}), so the upload must say which.");
        var target = pocs.Targets.FirstOrDefault(t => string.Equals(t.Name, named.Trim(), StringComparison.OrdinalIgnoreCase));
        return target is null ? (null, $"No target '{named}' is configured in pocs.targets ({names}).") : (target, null);
    }
}
