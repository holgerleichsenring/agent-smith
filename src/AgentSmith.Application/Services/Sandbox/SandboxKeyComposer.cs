namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Composes the operator-facing sandbox key from (repoCount, repoName,
/// perRepoDiscoveryCount, contextName) per p0161a:
///   single-repo, single-context  → "default" (or "&lt;ctx&gt;" if non-default)
///   single-repo, multi-context   → "&lt;ctx&gt;"
///   multi-repo,  single-context  → "&lt;repo&gt;"
///   multi-repo,  multi-context   → "&lt;repo&gt;/&lt;ctx&gt;"
/// </summary>
public static class SandboxKeyComposer
{
    private const string DefaultContextName = "default";

    public static string Compose(
        int repoCount, string repoName, int perRepoDiscoveryCount, string contextName)
    {
        var isMultiRepo = repoCount > 1;
        var isMultiContext = perRepoDiscoveryCount > 1;

        if (!isMultiRepo && !isMultiContext)
            return contextName;
        if (!isMultiRepo)
            return contextName;
        if (!isMultiContext)
            return repoName;
        return $"{repoName}/{contextName}";
    }
}
