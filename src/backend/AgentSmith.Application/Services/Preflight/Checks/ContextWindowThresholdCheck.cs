using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// 2026-08-27-3eb1: a compaction threshold that cannot fire before the provider refuses
/// is a configuration defect, and it is checkable without spending anything. With a role
/// stating <c>context_window_tokens</c>, <c>max_context_tokens × trigger_ratio ≥ window</c>
/// means the fold can never happen in time — exactly the state an installation was in when
/// four runs in a row died at 140k against a 128000 ceiling. A role that states no window
/// derives no threshold, and this check says which roles those are rather than failing an
/// installation that never opted in.
/// </summary>
public sealed class ContextWindowThresholdCheck(IPreflightConfigSource configSource) : IPreflightCheck
{
    public string Name => "context-window";

    public string Category => "llm";

    public Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var config = configSource.Resolve().Config;
        if (config is null)
            return Task.FromResult(
                PreflightCheckResult.Skip("agentsmith.yml failed to load — see config-schema"));
        if (config.Agents.Count == 0)
            return Task.FromResult(PreflightCheckResult.Skip("no agents configured"));

        var unreachable = new List<string>();
        var unstated = new List<string>();
        foreach (var (agentName, agent) in config.Agents)
            foreach (var (role, assignment) in Roles(agent.Models))
                Classify(agentName, role, assignment, agent.Compaction, unreachable, unstated);

        return Task.FromResult(Report(unreachable, unstated));
    }

    private static void Classify(
        string agentName, string role, ModelAssignment assignment, CompactionConfig compaction,
        List<string> unreachable, List<string> unstated)
    {
        var label = $"{agentName}.{role}";
        if (assignment.ContextWindowTokens is not { } window || window <= 0)
        {
            unstated.Add(label);
            return;
        }
        if (!compaction.IsEnabled || compaction.MaxContextTokensTriggerRatio <= 0) return;
        var trigger = (int)(compaction.MaxContextTokens * compaction.MaxContextTokensTriggerRatio);
        if (trigger >= window)
            unreachable.Add($"{label}: fold at {trigger} tokens, window {window}");
    }

    private static PreflightCheckResult Report(List<string> unreachable, List<string> unstated)
    {
        if (unreachable.Count > 0)
            return PreflightCheckResult.Fail(
                "compaction can never fire before the provider refuses — " + string.Join(" | ", unreachable),
                "Lower agents.<name>.compaction.max_context_tokens (or its "
                + "max_context_tokens_trigger_ratio) until threshold × ratio is below the role's "
                + "context_window_tokens. Note the trigger ratio is NOT editable in the config "
                + "studio — AgentCompactionSettings does not carry it — so edit agentsmith.yml "
                + "or lower max_context_tokens instead.");

        var stated = unstated.Count == 0
            ? "every model role states context_window_tokens"
            : $"no context_window_tokens stated for: {string.Join(", ", unstated)} — no compaction "
              + "threshold is derived for those roles and a sweep runs until the provider refuses";
        return PreflightCheckResult.Pass(stated);
    }

    private static IEnumerable<(string Role, ModelAssignment Assignment)> Roles(ModelRegistryConfig? models)
    {
        if (models is null) yield break;
        yield return ("scout", models.Scout);
        yield return ("primary", models.Primary);
        yield return ("planning", models.Planning);
        if (models.Reasoning is { } reasoning) yield return ("reasoning", reasoning);
        yield return ("summarization", models.Summarization);
        yield return ("contextGeneration", models.ContextGeneration);
        yield return ("codeMapGeneration", models.CodeMapGeneration);
    }
}
