namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: the names a render looks for and sets — the 'app' resources, the pod labels the selectors
/// match, the annotations agent smith owns and the generated ConfigMap's file.</summary>
public static class PocManifestNames
{
    public const string App = "app";
    public const string NameLabel = "app.kubernetes.io/name";
    public const string PocLabel = "agent-smith.io/poc";
    public const string SyncWave = "argocd.argoproj.io/sync-wave";

    /// <summary>The Deployment's wave: after the Namespace, the ConfigMap and the SealedSecrets of wave 0.</summary>
    public const string DeploymentWave = "1";

    public const string ChecksumAnnotation = "agent-smith.io/variables-checksum";
    public const string IssuerAnnotation = "cert-manager.io/cluster-issuer";
    public const string TlsSecret = "app-tls";
    public const string ConfigMapFile = "configmap.yaml";
    public const string AppFolder = "app";
}
