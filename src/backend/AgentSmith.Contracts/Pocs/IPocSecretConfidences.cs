namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dd: the confidence of every pattern of the skills catalog's 'secrets' catalog, by pattern id — what the
/// image stage reads to decide which Trivy secret rules block, so both stages block the same secrets. A catalog with no
/// secret patterns answers a failure naming where it looked, never an empty pass.
/// </summary>
public interface IPocSecretConfidences
{
    (IReadOnlyDictionary<string, int> ByPattern, string? Failure) Read();
}
