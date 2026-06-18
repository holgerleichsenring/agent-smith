namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0272: one operator-declared secret file mounted into the sandbox pod. The
/// referenced Kubernetes Secret key is projected as a single file at
/// <see cref="Mount"/> (read-only), so a build step can read it by path — e.g. a
/// Salesforce JWT key consumed by <c>sf org login jwt --jwt-key-file</c>. The
/// value never reaches a Step/Redis payload; only the Secret reference is sent to
/// the pod spec and Kubernetes resolves it at mount time.
/// </summary>
public sealed class SandboxSecretFile
{
    /// <summary>Absolute path the secret key is mounted at, e.g. <c>/secrets/server.key</c>.</summary>
    public string Mount { get; set; } = string.Empty;

    /// <summary>Name of the Kubernetes Secret holding the value.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Key within the Secret to project as the file's content.</summary>
    public string Key { get; set; } = string.Empty;
}
