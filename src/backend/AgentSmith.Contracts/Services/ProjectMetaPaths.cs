namespace AgentSmith.Contracts.Services;

/// <summary>
/// Repo-relative locations of agent-smith's project-meta artifacts (those that
/// live inside the consumer project's working tree under <c>.agentsmith/</c>).
/// Distinct from <see cref="IAgentSmithPaths"/>, which resolves host-side
/// cache locations outside any project. Centralising the path strings here
/// keeps the convention consistent across handlers, builders, and CLI verbs
/// — and makes a future renaming of <c>.agentsmith/</c> a single-file change.
/// </summary>
public static class ProjectMetaPaths
{
    /// <summary>The top-level meta directory inside a project repo.</summary>
    public const string Root = ".agentsmith";

    public const string ContextYaml = Root + "/context.yaml";
    public const string CodingPrinciples = Root + "/coding-principles.md";
    public const string ConfigYaml = Root + "/agentsmith.yml";
    public const string Runs = Root + "/runs";
    public const string Wiki = Root + "/wiki";
    public const string Security = Root + "/security";

    /// <summary>p0161a: per-context meta sub-tree under .agentsmith/contexts/.</summary>
    public const string Contexts = Root + "/contexts";

    public const string ContextYamlFile = "context.yaml";
    public const string CodingPrinciplesFile = "coding-principles.md";

    /// <summary>p0161a: absolute MetaDir inside the sandbox for one discovered
    /// context. /work prefix matches Repository.SandboxWorkPath. Result is
    /// /work/.agentsmith/contexts/&lt;contextName&gt;.</summary>
    public static string MetaDirFor(string contextName) =>
        $"/work/{Contexts}/{contextName}";
}
