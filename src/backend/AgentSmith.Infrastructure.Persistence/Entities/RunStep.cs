namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>One pipeline step's record: its index, name, status, duration and result line.</summary>
public sealed class RunStep : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    // p0344b: the TYPED command name (CommandNames constant, optionally with a
    // ":param" suffix) from StepStartedEvent — the deterministic input the
    // server-side run-story beat derivation maps on. Null on pre-p0344b rows,
    // which serve beats: null (no storybar) instead of a label-based guess.
    public string? CommandName { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? DurationSeconds { get; set; }
    public string? ResultMessage { get; set; }
}
