namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>
/// 2026-11-18-3863e: a ref as each host takes it. GitHub and GitLab take a branch or tag by its short name; Azure DevOps
/// takes the full ref, so a short name there is a branch.
/// </summary>
public static class PipelineRef
{
    private const string Heads = "refs/heads/";
    private const string Tags = "refs/tags/";

    public static string? Short(string? reference) =>
        string.IsNullOrWhiteSpace(reference) ? null
        : reference.StartsWith(Heads, StringComparison.Ordinal) ? reference[Heads.Length..]
        : reference.StartsWith(Tags, StringComparison.Ordinal) ? reference[Tags.Length..]
        : reference.Trim();

    public static string? Full(string? reference) =>
        string.IsNullOrWhiteSpace(reference) ? null
        : reference.StartsWith("refs/", StringComparison.Ordinal) ? reference.Trim()
        : Heads + reference.Trim();
}
