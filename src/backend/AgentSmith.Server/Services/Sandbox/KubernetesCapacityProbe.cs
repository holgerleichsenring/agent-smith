using System.Globalization;
using AgentSmith.Contracts.Sandbox;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0269a: reads the namespace ResourceQuota(s) and answers whether a sandbox of a
/// given footprint still fits (hard - used >= required) for every quota that
/// constrains cpu / memory / pods. If ANY quota would be exceeded, capacity is
/// denied with the offending resource named. A namespace with no ResourceQuota is
/// unconstrained → admit. Reads are fail-open: a transient API/RBAC read error
/// admits, because the pod-create itself remains the hard guard (a real quota
/// rejection there maps to CapacityExhaustedException).
/// </summary>
public sealed class KubernetesCapacityProbe(
    IKubernetes client,
    KubernetesSandboxOptions options,
    ILogger<KubernetesCapacityProbe> logger) : ISandboxCapacityProbe
{
    public async Task<CapacityDecision> HasCapacityAsync(ResourceLimits footprint, CancellationToken cancellationToken)
    {
        V1ResourceQuotaList quotas;
        try
        {
            quotas = await client.CoreV1.ListNamespacedResourceQuotaAsync(
                options.Namespace, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Capacity probe could not read ResourceQuota in namespace {Ns} — admitting (spawn stays the guard)",
                options.Namespace);
            return CapacityDecision.Admit();
        }

        return Evaluate(quotas.Items, footprint, options.Namespace);
    }

    // Pure: given the namespace quotas and the run footprint, decide fit. Extracted so
    // the hard-vs-used math is unit-tested without a k8s client mock.
    internal static CapacityDecision Evaluate(
        IList<V1ResourceQuota>? quotas, ResourceLimits footprint, string ns)
    {
        if (quotas is null || quotas.Count == 0)
            return CapacityDecision.Admit();

        foreach (var quota in quotas)
        {
            var hard = quota.Status?.Hard;
            var used = quota.Status?.Used;
            if (hard is null) continue;

            foreach (var need in RequiredAmounts(footprint))
            {
                if (!hard.TryGetValue(need.QuotaKey, out var hardQty)) continue;
                if (!TryParse(need.QuotaKey, hardQty.ToString(), out var hardVal)) continue;
                var usedVal = used is not null && used.TryGetValue(need.QuotaKey, out var usedQty)
                              && TryParse(need.QuotaKey, usedQty.ToString(), out var u) ? u : 0d;

                if (hardVal - usedVal < need.Required)
                {
                    return CapacityDecision.Deny(
                        $"Kubernetes ResourceQuota '{quota.Metadata?.Name}' in namespace "
                        + $"'{ns}' is at capacity for {need.QuotaKey} "
                        + $"(hard {hardQty}, used {FormatUsed(used, need.QuotaKey)}); waiting for room.");
                }
            }
        }

        return CapacityDecision.Admit();
    }

    // The footprint expressed as the quota resources it consumes. A quota may
    // constrain either the prefixed ("requests.cpu") or bare ("cpu") form; emit both
    // so whichever the quota uses matches. One pod is always consumed.
    private static IEnumerable<(string QuotaKey, double Required)> RequiredAmounts(ResourceLimits f)
    {
        if (KubernetesQuantity.TryParseCpuToNanoCpus(f.CpuRequest, out var cpuReq))
        {
            yield return ("requests.cpu", cpuReq);
            yield return ("cpu", cpuReq);
        }
        if (KubernetesQuantity.TryParseMemoryToBytes(f.MemoryRequest, out var memReq))
        {
            yield return ("requests.memory", memReq);
            yield return ("memory", memReq);
        }
        if (KubernetesQuantity.TryParseCpuToNanoCpus(f.CpuLimit, out var cpuLim))
            yield return ("limits.cpu", cpuLim);
        if (KubernetesQuantity.TryParseMemoryToBytes(f.MemoryLimit, out var memLim))
            yield return ("limits.memory", memLim);
        yield return ("pods", 1d);
        yield return ("count/pods", 1d);
    }

    private static bool TryParse(string quotaKey, string quantity, out double value)
    {
        if (quotaKey.EndsWith("cpu", StringComparison.Ordinal))
            return ParseToDouble(KubernetesQuantity.TryParseCpuToNanoCpus, quantity, out value);
        if (quotaKey.EndsWith("memory", StringComparison.Ordinal))
            return ParseToDouble(KubernetesQuantity.TryParseMemoryToBytes, quantity, out value);
        // pods / count/pods — a plain integer count.
        return double.TryParse(quantity, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private delegate bool TryParseLong(string? raw, out long parsed);

    private static bool ParseToDouble(TryParseLong parse, string quantity, out double value)
    {
        if (parse(quantity, out var l)) { value = l; return true; }
        value = 0d;
        return false;
    }

    private static string FormatUsed(IDictionary<string, ResourceQuantity>? used, string key) =>
        used is not null && used.TryGetValue(key, out var v) ? v.ToString() : "0";
}
