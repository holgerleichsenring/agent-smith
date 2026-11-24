namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dg: writes a PoC's stored secrets in the format its target reads, on use — the provider seam
/// pocs.targets[].secrets.provider chooses on. Sealed Secrets first; a vault write would be another provider here.
/// </summary>
public interface IPocSecretProvider
{
    /// <summary>The pocs.targets[].secrets.provider value this provider answers to.</summary>
    string Name { get; }

    PocSecretFile Render(PocSecretOrder order);

    /// <summary>2026-11-23-c81eb: the file name <see cref="Render"/> gives the object <paramref name="name"/> — what the
    /// GitOps commit looks for on the branch when the seal is unchanged.</summary>
    string FileNameOf(string name);
}
