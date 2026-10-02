namespace AgentSmith.Server.Services.References;

/// <summary>
/// 2026-10-02-075da: files that commonly hold credentials. They are KEPT — an env file says what
/// the material needs, and the operator chose to upload it — and named in the upload's answer, so
/// the composer can say the model and the run will read them. The list is a reminder, not a
/// scanner: a secret in any other file is stored the same way.
/// </summary>
public static class ReferenceCredentialFiles
{
    private static readonly string[] Exact = [".env", ".npmrc", "kubeconfig"];

    public static bool Holds(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = path.Split('/')[^1];
        return Exact.Contains(name, StringComparer.OrdinalIgnoreCase)
            || name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".env", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".pem", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".key", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("id_rsa", StringComparison.Ordinal)
            || (name.StartsWith("credentials", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }
}
