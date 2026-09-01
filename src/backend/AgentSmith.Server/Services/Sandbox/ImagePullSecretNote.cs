namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-08-31-46d7: the sentence an image-pull failure adds to the kubelet's own message.
/// <para>
/// Kubelet reports the registry's rejection but never reports a MISSING pull secret — a
/// pod referencing a secret that does not exist is not rejected, the entry is ignored and
/// the pull simply goes out unauthenticated. So the framework states what it configured,
/// and states it from the sandbox spec: nothing is read back from the cluster, because
/// confirming a secret exists would need read access to every secret in the sandbox
/// namespace — a real security regression bought for a better error string.
/// </para>
/// </summary>
public static class ImagePullSecretNote
{
    /// <summary>The waiting reasons that mean the image itself never arrived.</summary>
    private static readonly string[] PullReasons =
        ["ImagePullBackOff", "ErrImagePull", "InvalidImageName"];

    /// <summary>Empty for any other failure — a crash loop says nothing about credentials.</summary>
    public static string For(string reason, IReadOnlyList<string>? pullSecrets)
    {
        if (!PullReasons.Contains(reason)) return string.Empty;
        return pullSecrets is null or []
            ? " No image pull secret was configured for this sandbox"
              + " (sandbox.image_pull_secrets), so the pull went out unauthenticated."
            : $" Configured image pull secrets: {string.Join(", ", pullSecrets)}"
              + " (declared, not verified — a name matching no Secret is silently ignored).";
    }
}
