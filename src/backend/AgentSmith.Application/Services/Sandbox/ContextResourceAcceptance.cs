using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0268/p0320a: accepts or rejects the LLM-authored context.yaml stack.resources block,
/// and clamps what it accepts to the operator's ceiling. One responsibility, held apart
/// from the LAYER ORDER that <see cref="SandboxResourceResolver"/> owns.
/// </summary>
public sealed class ContextResourceAcceptance(
    IOptions<SandboxOptions> options,
    // Optional so the bare test construction sites keep compiling; when absent a rejected
    // block still falls back correctly, just without the WARN line.
    ILogger<ContextResourceAcceptance>? logger = null) : IContextResourceAcceptance
{
    // p0268: validate an LLM-authored stack.resources block before trusting it to size
    // the sandbox. All four fields must be present AND parse as Kubernetes quantities
    // (the SAME parse the Docker spawner uses). On any miss, return null so the caller
    // falls back to the global default, with a WARN so the rejection is visible — mirrors
    // p0265 TryAcceptContextImage.
    public ResourceLimits? Accept(ContextYamlStackResources? contextResources)
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
        return ClampToCeiling(new ResourceLimits(cpuRequest, cpuLimit, memoryRequest, memoryLimit));
    }

    // p0320a: hard ceiling on LLM-authored sizes — clamp, don't reject. An over-sized
    // guess still runs (at the ceiling) instead of silently falling back to a default
    // that may be too small; safety lives in the API, not in prompt discipline.
    // Requests AND limits are clamped so an inflated request can't hog scheduling
    // capacity either. Operator project overrides never pass through here.
    private ResourceLimits ClampToCeiling(ResourceLimits accepted)
    {
        var clamped = new ResourceLimits(
            ClampCpu(accepted.CpuRequest), ClampCpu(accepted.CpuLimit),
            ClampMemory(accepted.MemoryRequest), ClampMemory(accepted.MemoryLimit));
        if (clamped != accepted)
        {
            logger?.LogWarning(
                "p0320a: context.yaml stack.resources exceed the ceiling (cpu {MaxCpu} / memory {MaxMem}) — "
                + "clamped cpu {CpuReq}/{CpuLim} → {NewCpuReq}/{NewCpuLim}, "
                + "memory {MemReq}/{MemLim} → {NewMemReq}/{NewMemLim}.",
                options.Value.MaxCpuLimit, options.Value.MaxMemoryLimit,
                accepted.CpuRequest, accepted.CpuLimit, clamped.CpuRequest, clamped.CpuLimit,
                accepted.MemoryRequest, accepted.MemoryLimit, clamped.MemoryRequest, clamped.MemoryLimit);
        }
        return clamped;
    }

    private string ClampCpu(string value) =>
        KubernetesQuantity.TryParseCpuToNanoCpus(value, out var v)
        && KubernetesQuantity.TryParseCpuToNanoCpus(options.Value.MaxCpuLimit, out var max)
        && v > max ? options.Value.MaxCpuLimit : value;

    private string ClampMemory(string value) =>
        KubernetesQuantity.TryParseMemoryToBytes(value, out var v)
        && KubernetesQuantity.TryParseMemoryToBytes(options.Value.MaxMemoryLimit, out var max)
        && v > max ? options.Value.MaxMemoryLimit : value;
}
