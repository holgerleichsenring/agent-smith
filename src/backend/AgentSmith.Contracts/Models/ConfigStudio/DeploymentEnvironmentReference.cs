namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// 2026-11-18-3863a: the editable studio view of one environment of a deployment binding.
/// </summary>
/// <param name="Repo">A repo ref from the repos catalog holding the environment's manifest; null means the bound
/// context's own repository.</param>
/// <param name="Approval">Null reads as true: a person merges the delivery's pull request.</param>
public sealed record DeploymentEnvironmentReference(
    string Name,
    string? Repo = null,
    bool? Approval = null,
    IReadOnlyDictionary<string, string>? Parameters = null)
{
    public DeploymentEnvironmentReference() : this(string.Empty) { }
}
