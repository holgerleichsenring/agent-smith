namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0341c: constrains a written context_name to the repo's DISCOVERED contexts —
/// the invariant belongs in the write API, not the prompt. An invented name (e.g.
/// the example 'default') when real contexts exist is rejected, or redirected when
/// there is exactly one real context. A genuine bootstrap (no discovered contexts)
/// is unaffected. Extracted from WriteContextYamlToolHost (p0504).
/// </summary>
public sealed class ContextNameGuard(
    IReadOnlyDictionary<string, IReadOnlyList<string>>? discoveredContexts,
    string? defaultRepoName)
{
    /// <summary>
    /// Returns false with an error when the name is invented and cannot be safely
    /// redirected; may REWRITE <paramref name="contextName"/> to the single discovered context.
    /// </summary>
    public bool TryResolve(string repo, ref string contextName, out string? error)
    {
        error = null;
        if (discoveredContexts is null || discoveredContexts.Count == 0) return true; // bootstrap

        var repoName = string.IsNullOrEmpty(repo) ? (defaultRepoName ?? string.Empty) : repo;
        if (!discoveredContexts.TryGetValue(repoName, out var keys) || keys is null || keys.Count == 0)
            return true; // no discovery for this repo => genuine bootstrap, any name allowed

        var requested = contextName; // ref params cannot be captured in a lambda
        if (keys.Any(k => string.Equals(k, requested, StringComparison.OrdinalIgnoreCase)))
            return true; // the model named a real discovered context

        if (keys.Count == 1)
        {
            // Exactly one real context — redirect the invented name to it rather than
            // authoring a stray sibling.
            contextName = keys[0];
            return true;
        }

        error = $"Error: context_name '{contextName}' is not a discovered context for repo "
            + $"'{(string.IsNullOrEmpty(repoName) ? "(default)" : repoName)}'. Use one of the "
            + $"resolved contexts: [{string.Join(", ", keys)}]. Do not invent a new context name "
            + "(e.g. the example 'default') when real contexts exist.";
        return false;
    }
}
