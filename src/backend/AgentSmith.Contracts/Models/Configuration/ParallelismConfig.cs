namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Controls fan-out of same-stage skill rounds in PipelineExecutor.
/// Default 1 = sequential (current behavior). Higher values batch consecutive
/// commands that share (Name, Round) and run them in parallel under a SemaphoreSlim
/// throttle, with stage barriers preserved.
/// </summary>
public sealed class ParallelismConfig
{
    public int MaxConcurrentSkillRounds { get; set; } = 1;
}
