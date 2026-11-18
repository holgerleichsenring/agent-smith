namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-18-3863c: what the server seeds into a 'deploy' run — strings, so the request survives the queue.</summary>
public static partial class ContextKeys
{
    /// <summary>The context of the project whose deployment binding is delivered.</summary>
    public const string DeployContext = "DeployContext";

    /// <summary>Which repository of the project that context lives in, when two declare its name; may be absent.</summary>
    public const string DeployContextRepo = "DeployContextRepo";

    /// <summary>The environment the version goes to.</summary>
    public const string DeployEnvironment = "DeployEnvironment";

    /// <summary>The version to deliver; absent asks the run to promote it from the environment the template names.</summary>
    public const string DeployVersion = "DeployVersion";

    /// <summary>2026-11-18-3863h: the version a settled deploy of the source environment delivered, seeded only by the
    /// server's promotion — never by a person, whose typed version for a promoted environment is refused.</summary>
    public const string DeployPromotedVersion = "DeployPromotedVersion";
}
