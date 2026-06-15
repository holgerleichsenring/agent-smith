using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Three-layer resolver (p0268): per-project SandboxConfig.Resources (operator) wins;
/// otherwise the LLM-authored context.yaml stack.resources block applies once validated;
/// otherwise the global SandboxOptions default. Partial overrides are not supported at any
/// layer — each is taken wholesale. A context block that is partial (not all four fields)
/// or whose quantities do not parse is rejected WHOLE and falls through to the global default
/// with a WARN, so a bad LLM guess never silently mis-sizes a sandbox.
/// </summary>
public sealed class SandboxResourceResolver(
    IOptions<SandboxOptions> options,
    // Optional so the many bare test construction sites keep compiling; when absent a
    // rejected context block still falls back correctly, just without the WARN line.
    ILogger<SandboxResourceResolver>? logger = null) : ISandboxResourceResolver
{
    public ResourceLimits Resolve(ResolvedProject projectConfig, ContextYamlStackResources? contextResources = null)
    {
        // 1. Operator project override — wins outright (operator authority beats the LLM guess).
        if (projectConfig.Sandbox?.Resources is { } projectOverride) return projectOverride;

        // 2. LLM-authored context.yaml stack.resources — applied only when valid.
        if (TryAcceptContextResources(contextResources) is { } accepted) return accepted;

        // 3. Global default.
        return options.Value.ToResourceLimits();
    }

    // p0268: validate an LLM-authored stack.resources block before trusting it to size
    // the sandbox. All four fields must be present AND parse as Kubernetes quantities
    // (the SAME parse the Docker spawner uses). On any miss, return null so the caller
    // falls back to the global default, with a WARN so the rejection is visible — mirrors
    // p0265 TryAcceptContextImage.
    private ResourceLimits? TryAcceptContextResources(ContextYamlStackResources? contextResources)
    {
        if (contextResources is null) return null;

        var cpuRequest = contextResources.CpuRequest?.Trim();
        var cpuLimit = contextResources.CpuLimit?.Trim();
        var memoryRequest = contextResources.MemoryRequest?.Trim();
        var memoryLimit = contextResources.MemoryLimit?.Trim();

        if (string.IsNullOrEmpty(cpuRequest) || string.IsNullOrEmpty(cpuLimit)
            || string.IsNullOrEmpty(memoryRequest) || string.IsNullOrEmpty(memoryLimit))
        {
            logger?.LogWarning(
                "p0268: context.yaml stack.resources is partial (cpu_request='{CpuReq}' cpu_limit='{CpuLim}' "
                + "memory_request='{MemReq}' memory_limit='{MemLim}') — all four are required. "
                + "Falling back to the global sandbox default.",
                cpuRequest ?? "null", cpuLimit ?? "null", memoryRequest ?? "null", memoryLimit ?? "null");
            return null;
        }

        if (!KubernetesQuantity.TryParseCpuToNanoCpus(cpuRequest, out _)
            || !KubernetesQuantity.TryParseCpuToNanoCpus(cpuLimit, out _)
            || !KubernetesQuantity.TryParseMemoryToBytes(memoryRequest, out _)
            || !KubernetesQuantity.TryParseMemoryToBytes(memoryLimit, out _))
        {
            logger?.LogWarning(
                "p0268: context.yaml stack.resources has an unparseable Kubernetes quantity "
                + "(cpu_request='{CpuReq}' cpu_limit='{CpuLim}' memory_request='{MemReq}' memory_limit='{MemLim}'). "
                + "Falling back to the global sandbox default.",
                cpuRequest, cpuLimit, memoryRequest, memoryLimit);
            return null;
        }

        logger?.LogInformation(
            "p0268: using LLM-authored context.yaml stack.resources (cpu {CpuReq}/{CpuLim}, memory {MemReq}/{MemLim}).",
            cpuRequest, cpuLimit, memoryRequest, memoryLimit);
        return new ResourceLimits(cpuRequest, cpuLimit, memoryRequest, memoryLimit);
    }
}
