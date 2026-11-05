namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// The knobs of the DIALOG surface — a design or steward turn, which reads code and uploads and answers in prose,
/// and which replays the previous turn's thread, so it needs a window to fold against.
/// </summary>
public sealed partial class AgentConfig
{
    /// <summary>The per-turn tool-iteration ceiling of a dialog master. Raise via
    /// <c>agent.dialog_master_loop_iterations</c>.</summary>
    public int DialogMasterLoopIterations { get; set; } = 100;

    /// <summary>The output budget a dialog turn is re-prompted under once its reply was cut at the role's own limit.
    /// Raise via <c>agent.dialog_master_max_output_tokens</c>.</summary>
    public int DialogMasterMaxOutputTokens { get; set; } = 32000;

    /// <summary>The input window a dialog turn runs against when the resolved role states none. A role that states
    /// <c>context_window_tokens</c> wins; set &lt;= 0 to state nothing, which also stops the thread replay.</summary>
    public int DialogContextWindowTokens { get; set; } = 200000;
}
