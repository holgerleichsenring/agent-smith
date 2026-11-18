namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: what a deployment template declares that a deploy run acts on — how a version is delivered and
/// the environments it can go to. The files, parameters and secrets the declaration also names are read by the coding
/// master (2026-11-18-3863b), not by the run.
/// </summary>
public sealed record DeploymentDeclaration(string Delivery, IReadOnlyList<DeclaredEnvironment> Environments)
{
    /// <summary>A pull request to the environment's manifest; the one delivery this series builds.</summary>
    public const string GitOpsPullRequest = "gitops_pr";

    /// <summary>The environment the template declares under <paramref name="name"/>, or null.</summary>
    public DeclaredEnvironment? Environment(string name) =>
        Environments.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
}
