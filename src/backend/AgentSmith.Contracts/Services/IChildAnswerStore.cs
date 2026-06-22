namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0280: per-run, in-memory store of each sub-agent's final answer text, keyed by
/// sub-agent id. The functional child→master detail channel: SubAgentRunner stores a
/// child's answer when its loop ends, and read_sub_agent_observations reads it back so
/// the master can pull a child's findings and synthesise them. Replaces the never-built
/// Redis IRunEventReader path for the functional flow (events stay best-effort for the
/// dashboard). Scoped per pipeline run.
/// </summary>
public interface IChildAnswerStore
{
    void Store(string subAgentId, string answer);

    bool TryGet(string subAgentId, out string answer);
}
