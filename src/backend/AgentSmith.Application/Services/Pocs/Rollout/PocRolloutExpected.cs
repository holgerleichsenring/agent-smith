namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>
/// 2026-11-23-c81ec: what a follow waits for on the PoC's Application (named like its namespace, poc-&lt;name&gt;) —
/// either its absence (poc-delete), or a live Deployment 'app' whose pod template runs
/// &lt;registry&gt;/&lt;repository&gt;@&lt;digest&gt; with the variables checksum, at the desired replica count.
/// </summary>
public sealed record PocRolloutExpected(
    string Application, string Namespace, bool Absent, string? Registry, string? Repository, string? Digest, int Replicas,
    string? Checksum)
{
    public static PocRolloutExpected Gone(string application) => new(application, application, true, null, null, null, 0, null);
}
