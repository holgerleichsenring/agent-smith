namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// The knobs of the DIALOG surface — a design or steward turn, which reads code and uploads and answers in prose,
/// and which replays the previous turn's thread, so it needs a window to fold against.
/// </summary>
public sealed partial class AgentConfig
{
    /// <summary>The per-turn tool-iteration ceiling of a dialog master, via <c>agent.dialog_master_loop_iterations</c>.
    /// 2026-11-12-cd4cl: the run master's default; the turn's cost cap is the real stop.</summary>
    public int DialogMasterLoopIterations { get; set; } = 200;

    /// <summary>2026-11-12-cd4cl: the per-pass ceiling of a child a design turn spawns, via
    /// <c>agent.dialog_sub_agent_loop_iterations</c>; the run child's default.</summary>
    public int DialogSubAgentLoopIterations { get; set; } = 100;

    /// <summary>2026-11-12-cd4cl: the looks a design turn's proposal and plan reviewers may take, via
    /// <c>agent.dialog_review_look_allowance</c>; the attack reviewer's floor.</summary>
    public int DialogReviewLookAllowance { get; set; } = 24;

    /// <summary>2026-11-12-cd4cl: the transcript window a dialog turn's prompt and its digest fold against, via
    /// <c>agent.dialog_transcript</c>.</summary>
    public TranscriptWindowConfig DialogTranscript { get; set; } = new();

    /// <summary>The input window a dialog turn runs against when the resolved role states none. A role that states
    /// <c>context_window_tokens</c> wins; set &lt;= 0 to state nothing, which also stops the thread replay.</summary>
    public int DialogContextWindowTokens { get; set; } = 200000;
}
