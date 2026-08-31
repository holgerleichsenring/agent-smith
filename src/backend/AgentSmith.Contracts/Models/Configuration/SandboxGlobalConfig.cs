namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Process-wide sandbox defaults, loaded from agentsmith.yml's top-level
/// <c>sandbox:</c> block. Per-project <see cref="SandboxConfig"/> blocks
/// override these field-by-field.
/// </summary>
public sealed class SandboxGlobalConfig
{
    /// <summary>
    /// Container registry the sandbox agent image is pulled from
    /// (e.g. <c>holgerleichsenring</c>, <c>ghcr.io/my-org</c>, or a private
    /// mirror). Combined with the constant image-name and <see cref="AgentVersion"/>
    /// to form the fully-qualified image reference.
    /// </summary>
    public string AgentRegistry { get; set; } = AgentSmith.Contracts.Constants.AgentImageDefaults.DefaultRegistry;

    /// <summary>
    /// Sandbox agent image tag (e.g. <c>0.48.0</c>). 2026-08-25-0d01: an OVERRIDE, not a
    /// requirement — left empty (and with no <c>deployment.version</c> filling it), the tag
    /// is DERIVED from the release the running server is, so the two cannot drift apart by
    /// being forgotten. Set it to pin a different published tag deliberately: an air-gapped
    /// mirror carrying one release, or a bisecting developer. A deliberate pin is reported
    /// as an advisory finding, never refused.
    /// </summary>
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>
    /// p0200: per-sandbox-step wall-time cap in seconds. Caps any incoming
    /// <c>Step.TimeoutSeconds</c> before the container backend computes its
    /// channel-wait (channel-wait stays cap + 30s grace).
    ///
    /// Default 900 (15 min) accommodates real-world C# / Node test suites:
    /// Sample's integration tests need ~5-10 min for restore + build +
    /// run inside a clean DockerSandbox; the prior 300s (the retired Test
    /// step) / 120s (initial p0200 draft) defaults wedged the operator's
    /// first successful registry-auth run mid-test on 2026-06-02.
    /// Operators tuning for fast-failure on micro-services lower this in
    /// agentsmith.yml's top-level <c>sandbox:</c> block.
    /// </summary>
    public int StepTimeoutSeconds { get; set; } = 900;

    /// <summary>
    /// p0230: default wall-time (seconds) for an agent <c>run_command</c> when the
    /// agent does not pass its own timeout. The prior hard-coded 60s killed real
    /// `dotnet restore` / `dotnet build` (minutes on a real solution) at the 60s
    /// mark, and the resulting cancellation failed the whole run. 300 (5 min)
    /// covers typical restore+build; per-project override via
    /// <see cref="SandboxConfig.RunCommandTimeoutSeconds"/>. Always bounded by
    /// <see cref="StepTimeoutSeconds"/> at the backend — a command can never
    /// outlive the step cap.
    /// </summary>
    public int RunCommandTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 2026-08-25-014d: the registries a sandbox toolchain image may be pulled from,
    /// as reference prefixes (<c>mcr.microsoft.com/</c>, <c>ghcr.io/</c>, a private
    /// mirror's host). The image string is model-authored or profile-authored, so
    /// this is the supply-chain boundary the run is held to. EMPTY means the built-in
    /// default, which is exactly what shipped before this field existed.
    /// </summary>
    public List<string> AllowedRegistries { get; set; } = [];

    /// <summary>
    /// 2026-08-25-014d: whether the Docker Hub official <em>library</em> namespace is
    /// trusted — <c>node:20-bookworm</c>, <c>buildpack-deps:bookworm-scm</c>. This is a
    /// repository SHAPE (no namespace segment), not a registry, so it carries its own
    /// switch: folding it into <see cref="AllowedRegistries"/> as a host entry would
    /// admit every user repository on that host instead.
    /// <para>
    /// NULL follows the registries. With none named the shape is trusted, so an unset
    /// configuration is the pre-existing policy unchanged; once a registry list IS
    /// named the shape is refused unless this says otherwise, because a named list is
    /// a narrowing and keeping the shape would silently widen it back open.
    /// </para>
    /// </summary>
    public bool? AllowDockerHubLibrary { get; set; }

    /// <summary>
    /// 2026-08-31-46d7: names of operator-created Kubernetes image pull secrets the
    /// sandbox pod references, so an image that only exists in a credentialed registry
    /// — the only place a licensed third-party tool may live — is pullable without
    /// patching the namespace's default service account out of band, where nobody can
    /// see it. A LIST because one pod pulls an agent image and a toolchain image that
    /// may come from different registries, and a pod-level reference covers the init
    /// container too. Global and never per-project: a project that could name its own
    /// credential would be widening a boundary it does not own.
    /// <para>
    /// Kubernetes only. The Docker backend pulls with no auth config at all and says so
    /// when a pull fails; nothing here is ever read back from the cluster, because
    /// verifying a secret exists would need read access to every secret in the namespace.
    /// </para>
    /// </summary>
    public List<string> ImagePullSecrets { get; set; } = [];

    // p0270a: the per-project override arithmetic that lived here
    // (ResolveStepTimeout / ResolveRunCommandTimeout) moved into the single
    // ConfigResolutionPass so the run path and the dashboard read one resolution.
}
