namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// The knobs that belong to the SCAN/REVIEW surface — the master whose declared
/// output schema is an observation array. They are separated from the coding
/// surface's because the two surfaces answer different questions: one produces a
/// change and verifies it, the other reads a lot of source and writes one array.
/// </summary>
public sealed partial class AgentConfig
{
    /// <summary>
    /// p0279: minimum distinct source files a scan/review master should read before its
    /// review is considered non-shallow. Below this floor the master is re-prompted ONCE
    /// to inventory the full surface and review each area. Default 6; raise via
    /// <c>agent.scan_min_source_reads</c> for large targets.
    /// </summary>
    public int ScanMinSourceReads { get; set; } = 6;

    /// <summary>
    /// 2026-09-01-6c32: the output budget the scan master's CLOSING ANSWER is written
    /// under. The primary role is configured for a coding turn (8192 output tokens), while
    /// the observation schema permits 500 characters of description, 300 of suggestion and
    /// 4000 of details per finding — a triage of a few dozen findings does not fit, and the
    /// array is cut off mid-write. Applies only to the observation-schema surface, so the
    /// coding master's budget is untouched. Raise via
    /// <c>agent.scan_master_max_output_tokens</c>.
    /// </summary>
    public int ScanMasterMaxOutputTokens { get; set; } = 32000;
}
