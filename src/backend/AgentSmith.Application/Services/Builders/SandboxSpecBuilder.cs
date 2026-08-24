using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Assembles a <see cref="SandboxSpec"/> from ResolvedProject + the context a
/// sandbox serves. Which image wins is <see cref="SandboxImageChain"/>'s
/// responsibility; resources, agent image, step cap and secrets are resolved
/// through their own services, so this type only composes the record.
/// </summary>
public sealed class SandboxSpecBuilder(
    ISandboxResourceResolver resourceResolver,
    IAgentImageResolver agentImageResolver,
    // p0230: optional so the many test construction sites keep compiling; when
    // absent the step cap resolves against fresh SandboxGlobalConfig defaults.
    Microsoft.Extensions.Options.IOptions<SandboxGlobalConfig>? globalConfig = null,
    // p0270a: the single config resolver provides the effective step timeout
    // (override ?? global) with provenance. Optional so the many bare test
    // construction sites keep compiling; when absent the step cap falls back to
    // the same inline arithmetic the deleted SandboxGlobalConfig.ResolveStepTimeout used.
    Configuration.IConfigResolver? configResolver = null,
    // p0272: parses the operator's sandbox.secrets block onto the spec. Optional
    // so the bare test construction sites keep compiling; the resolver is pure
    // (no deps), so the inline default matches the DI-registered instance.
    Sandbox.ISandboxSecretsResolver? secretsResolver = null,
    // p0504: the image ordering, extracted. Optional for the same reason the
    // others are — the chain is pure apart from an optional logger, so the
    // inline default behaves identically to the DI-registered instance.
    SandboxImageChain? imageChain = null)
{
    private readonly SandboxGlobalConfig _global = globalConfig?.Value ?? new SandboxGlobalConfig();
    private readonly Sandbox.ISandboxSecretsResolver _secretsResolver =
        secretsResolver ?? new Sandbox.SandboxSecretsResolver();
    private readonly SandboxImageChain _imageChain = imageChain ?? new SandboxImageChain();

    public SandboxSpec Build(
        ResolvedProject projectConfig, string? language, string? pipelineName,
        string? contextImage = null, ContextYamlStackResources? contextResources = null,
        // p0504: the image the declared meta.domain's profile brings. Reached only
        // when the context named no image of its own.
        string? profileImage = null)
    {
        ArgumentNullException.ThrowIfNull(projectConfig);
        var image = _imageChain.Resolve(projectConfig, language, contextImage, profileImage);
        // p0268: context.yaml stack.resources sizes the sandbox as a layer between the
        // operator project override and the global default (validated in the resolver).
        // p0320a: the pipeline name makes sizing pipeline-aware — only code-changing
        // pipelines consume the build sizing; the rest get the light profile.
        var resources = resourceResolver.Resolve(projectConfig, pipelineName, contextResources);
        var agentImage = agentImageResolver.Resolve(projectConfig);
        // p0230/p0270a: the per-step wall-time cap (project override ?? global) now
        // comes from the single ConfigResolver so the spec carries exactly what the
        // dashboard shows. The inline fallback covers bare test construction sites
        // that don't inject a resolver — identical to the retired ResolveStepTimeout.
        var stepTimeout = configResolver?.ResolveStepTimeout(projectConfig).Value
            ?? (projectConfig.Sandbox?.StepTimeoutSeconds ?? _global.StepTimeoutSeconds);
        // p0272: parse the operator's sandbox.secrets onto the spec (fail-fast on a
        // malformed reference); PodSpecBuilder turns these into secretKeyRef env +
        // Secret-volume mounts. Null/absent block resolves to ResolvedSandboxSecrets.Empty.
        var secrets = _secretsResolver.Resolve(projectConfig.Sandbox);
        return new SandboxSpec(
            ToolchainImage: image, Resources: resources, AgentImage: agentImage,
            StepTimeoutSeconds: stepTimeout, Secrets: secrets);
    }
}
