using System.Collections.Concurrent;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0280: in-memory <see cref="IChildAnswerStore"/> — a ConcurrentDictionary keyed by
/// sub-agent id. Scoped per run (one pipeline-execution DI scope == one run), so answers
/// from one run never leak into another and no eviction is needed.
/// </summary>
public sealed class InMemoryChildAnswerStore : IChildAnswerStore
{
    private readonly ConcurrentDictionary<string, string> _answers = new(StringComparer.Ordinal);

    public void Store(string subAgentId, string answer)
    {
        if (string.IsNullOrEmpty(subAgentId)) return;
        _answers[subAgentId] = answer ?? string.Empty;
    }

    public bool TryGet(string subAgentId, out string answer)
    {
        if (!string.IsNullOrEmpty(subAgentId) && _answers.TryGetValue(subAgentId, out var stored))
        {
            answer = stored;
            return true;
        }
        answer = string.Empty;
        return false;
    }
}
