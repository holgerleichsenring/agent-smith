using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Configuration.Resolved;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Four-layer resolver (p0268): per-project SandboxConfig.Resources (operator) wins;
/// otherwise the LLM-authored context.yaml stack.resources block applies once validated;
/// otherwise the global SandboxOptions default. Partial overrides are not supported at any
/// layer — each is taken wholesale. A context block that is partial (not all four fields)
/// or whose quantities do not parse is rejected WHOLE by
/// <see cref="IContextResourceAcceptance"/> and falls through to the global default with a
/// WARN, so a bad LLM guess never silently mis-sizes a sandbox.
/// p0320a: layers 2 and 3 apply only to code-changing pipelines — sizing asks WHAT the
/// sandbox will do, and only fix-bug/add-feature/… actually build; everything else
/// (init-project, scans, legal, mad) resolves to the fixed light profile.
/// </summary>
public sealed class SandboxResourceResolver(
    IOptions<SandboxOptions> options,
    IContextResourceAcceptance acceptance) : ISandboxResourceResolver
{
    public ResourceLimits Resolve(
        ResolvedProject projectConfig, string? pipelineName,
        ContextYamlStackResources? contextResources = null) =>
        Answer(projectConfig, pipelineName, contextResources).Limits;

    public SandboxResourceLayer ResolveLayer(
        ResolvedProject projectConfig, string? pipelineName,
        ContextYamlStackResources? contextResources = null) =>
        Answer(projectConfig, pipelineName, contextResources).Layer;

    // 2026-09-22-6c46: the value and the layer that produced it come out of ONE walk, so
    // a control that names the layer can never name a different one than the value came
    // from. The layer is not recoverable from the value — a light profile and a global
    // default can hold the same numbers — so it is returned alongside it.
    private (ResourceLimits Limits, SandboxResourceLayer Layer) Answer(
        ResolvedProject projectConfig, string? pipelineName,
        ContextYamlStackResources? contextResources)
    {
        // 1. Operator project override — wins outright for EVERY pipeline
        //    (operator authority beats the LLM guess and the light profile).
        if (projectConfig.Sandbox?.Resources is { } projectOverride)
            return (projectOverride, SandboxResourceLayer.ProjectOverride);

        // p0320a: non-code-changing pipelines clone + read/write files but never
        // compile, so neither the LLM-authored build sizing nor the build-capable
        // global default applies — they get the fixed light profile. A null/unknown
        // pipeline is treated the same: build sizing must be asked for explicitly.
        if (pipelineName is null || !PipelinePresets.ExpectsCodeChanges(pipelineName))
            return (ResourceLimits.LightProfile, SandboxResourceLayer.LightProfile);

        // 2. LLM-authored context.yaml stack.resources — applied only when valid,
        //    clamped to the SandboxOptions ceiling (p0320a).
        if (acceptance.Accept(contextResources) is { } accepted)
            return (accepted, SandboxResourceLayer.ContextDocument);

        // 3. Global default.
        return (options.Value.ToResourceLimits(), SandboxResourceLayer.GlobalDefault);
    }
}
