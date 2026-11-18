namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-18-3863a: one environment a deployment binding delivers to — what THIS project says about it. The template
/// says where its manifest is and what it promotes from; the binding says which repository holds it, whether a person
/// approves a delivery, and the values of the template's parameters.
/// </summary>
/// <param name="Repo">The repository the environment's manifest lives in, resolved from the repos catalog; null means
/// the bound context's own repository.</param>
/// <param name="Approval">True (the default): agent-smith opens the delivery's pull request and a person merges it.
/// False: agent-smith merges it too.</param>
public sealed record DeploymentEnvironment(
    string Name,
    RepoConnection? Repo,
    bool Approval,
    IReadOnlyDictionary<string, string> Parameters);
