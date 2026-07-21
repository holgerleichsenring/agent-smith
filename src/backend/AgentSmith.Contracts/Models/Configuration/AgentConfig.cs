namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for an AI agent provider (Claude, OpenAI, Gemini, Ollama).
/// </summary>
public sealed class AgentConfig
{
    public string Type { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? Deployment { get; set; }
    public string? ApiVersion { get; set; }
    public string? ApiKeySecret { get; set; }
    public RetryConfig Retry { get; set; } = new();
    public CacheConfig Cache { get; set; } = new();
    public CompactionConfig Compaction { get; set; } = new();
    public ModelRegistryConfig? Models { get; set; }
    public PricingConfig Pricing { get; set; } = new();
    public ParallelismConfig Parallelism { get; set; } = new();
    public RateLimitConfig? RateLimit { get; set; }

    /// <summary>
    /// p0235: per-request network timeout for a single LLM HTTP call, in
    /// seconds. The Azure/OpenAI SDK (System.ClientModel) defaults
    /// <c>NetworkTimeout</c> to 100s — a large gpt-4.1 completion carrying a
    /// big analyze-code context routinely exceeds that, and the SDK then throws
    /// a TaskCanceledException that surfaces as a bare "A task was canceled."
    /// Default 300s; still bounded in practice by
    /// <c>limits.max_seconds_per_skill_call</c> and
    /// <c>sandbox.step_timeout_seconds</c>.
    /// </summary>
    public int NetworkTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// p0258: how many times the coding master may RE-ATTEMPT after its own
    /// build/tests come back red before it gives up — surfaced to the master
    /// skill as the {MaxFixIterations} prompt variable. A run whose edit broke a
    /// test (or whose test now asserts the old behaviour) must investigate and
    /// fix, not stop at the first red — but bounded so a hopeless loop ends.
    /// Default 3, so no config is needed when 3 fits; raise via
    /// <c>agent.max_fix_iterations</c> for harder tickets.
    /// </summary>
    public int MaxFixIterations { get; set; } = 3;

    /// <summary>
    /// p0317: whether the configured model accepts image content parts. When true
    /// (the default — every current hosted flagship is multimodal), ticket image
    /// attachments are sent as image parts on the master's user message; when false
    /// (e.g. a text-only local Ollama model), the prompt carries a "N images
    /// attached, not viewable" note instead. Set via <c>agent.supports_vision</c>.
    /// </summary>
    public bool SupportsVision { get; set; } = true;

    /// <summary>
    /// p0279: minimum distinct source files a scan/review master should read before its
    /// review is considered non-shallow. Below this floor the master is re-prompted ONCE
    /// to inventory the full surface and review each area. Default 6; raise via
    /// <c>agent.scan_min_source_reads</c> for large targets.
    /// </summary>
    public int ScanMinSourceReads { get; set; } = 6;

    /// <summary>
    /// p0341c: the master loop's anti-runaway SAFETY ceiling — the maximum tool
    /// iterations one master pass may run before the harness forcibly stops it. This
    /// is NOT the stopping control (money + verification are — see the cost budget and
    /// the keystone); it only bounds a pathological runaway. Set well above the legacy
    /// 25 so the model can actually work a bulk cross-repo step to completion; the
    /// budget binds first in every healthy run. Raise via
    /// <c>agent.max_master_loop_iterations</c>.
    /// </summary>
    public int MaxMasterLoopIterations { get; set; } = 200;

    /// <summary>
    /// p0341c: the per-pass tool-iteration ceiling for a spawned SUB-agent. A child
    /// carrying a bulk "replace-all-X across repo Y" chunk previously inherited the 25
    /// default and stopped mid-way, so the master could not effectively fan out exactly
    /// the bulk steps that most need it. A generous real budget (still an anti-runaway
    /// net, not the control) lets a child finish its slice. Raise via
    /// <c>agent.max_sub_agent_loop_iterations</c>.
    /// </summary>
    public int MaxSubAgentLoopIterations { get; set; } = 100;

    /// <summary>
    /// p0341c/p0359: after this many consecutive tool iterations WITHOUT an
    /// update_progress call, the harness injects a ledger discipline reminder INTO the
    /// running master pass — the system-reminder analogue that keeps the model on the
    /// checklist and attacks early self-termination. Staleness-based: a model that keeps
    /// its ledger current is never nagged. Also fires on drift (see
    /// <see cref="ReminderDriftEditlessIterations"/>). Default 10; set &lt;= 0 to disable.
    /// </summary>
    public int LedgerReminderEveryNIterations { get; set; } = 10;

    /// <summary>
    /// p0341c: drift signal — after this many consecutive tool iterations with reads but
    /// NO edit/write, the harness injects the ledger reminder early (the model is
    /// spinning without moving the work). Default 8; set &lt;= 0 to disable the drift
    /// trigger (the staleness trigger still fires).
    /// </summary>
    public int ReminderDriftEditlessIterations { get; set; } = 8;

    /// <summary>
    /// p0360: minimum seconds between mid-run checkpoint pushes. On every accepted
    /// update_progress replace the framework commits + pushes each dirty repo sandbox's
    /// working tree to the run branch (secret-scanned, same gate as the final commit), so
    /// a run killed mid-flight — OOM'd sandbox, wall-time, crash — loses at most the work
    /// since the last checkpoint instead of everything. This throttles the push rate on
    /// runs that flip ledger steps rapidly. Set &lt;= 0 to disable checkpoint pushes.
    /// </summary>
    public int CheckpointPushMinIntervalSeconds { get; set; } = 120;
}

/// <summary>
/// p0188: per-agent rate-limit override. When unset, ChatClientFactory picks
/// a conservative default based on agent type (subscription tokens get a
/// tighter budget than API keys).
/// </summary>
public sealed class RateLimitConfig
{
    public int? RequestsPerMinute { get; set; }
    public int? InputTokensPerMinute { get; set; }
}
