using System.Text.RegularExpressions;
using AgentSmith.Domain.Exceptions;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992b: creates a sandbox pod and waits until every toolchain container in it is ready — what a single
/// sandbox and a run's workspace share, so neither factory holds a copy. A ResourceQuota rejection becomes a
/// <see cref="CapacityExhaustedException"/> here, where the Kubernetes status is still structured.
/// </summary>
public sealed partial class KubernetesPodLauncher(
    IKubernetes client, KubernetesSandboxOptions options, ILoggerFactory loggerFactory)
{
    public async Task LaunchAsync(
        V1Pod pod, TimeSpan timeout, IReadOnlyList<string>? pullSecrets, CancellationToken cancellationToken)
    {
        var podName = pod.Metadata.Name;
        try
        {
            await client.CoreV1.CreateNamespacedPodAsync(pod, options.Namespace, cancellationToken: cancellationToken);
        }
        catch (k8s.Autorest.HttpOperationException ex)
            when (ex.Response.StatusCode == System.Net.HttpStatusCode.Forbidden
                  && TryMapQuotaMessage(ExtractStatusMessage(ex), options.Namespace, ex, out var capacityEx))
        {
            // p0269a: a ResourceQuota rejection is a first-class CAPACITY signal, not a
            // fatal error. Translate it at THIS boundary (where the k8s status is
            // structured) into the typed exception so the core requeues the run as
            // waiting instead of terminal-failing it. Non-quota 403s (RBAC) fall through.
            throw capacityEx!;
        }
        loggerFactory.CreateLogger<KubernetesPodLauncher>()
            .LogInformation("Sandbox pod {Pod} created in namespace {Ns}", podName, options.Namespace);

        var watcher = new KubernetesPodWatcher(client, loggerFactory.CreateLogger<KubernetesPodWatcher>());
        var containers = pod.Spec.Containers.Select(c => c.Name).ToList();
        await watcher.WaitForReadyAsync(podName, options.Namespace, containers, timeout, pullSecrets, cancellationToken);
    }

    // p0269a: a ResourceQuota rejection is a 403 Forbidden whose V1Status message
    // reads "exceeded quota: <name>, requested: <resource>=<n>, used: ..., limited: ...".
    // An RBAC 403 is also Forbidden but carries no "exceeded quota" marker — those are
    // genuine failures and must NOT be mapped to a capacity signal. Pure so it is unit-
    // tested without a k8s client mock; this is provider-boundary translation of a
    // structured status, not a core message heuristic.
    internal static bool TryMapQuotaMessage(
        string? message, string ns, Exception? inner, out CapacityExhaustedException? mapped)
    {
        mapped = null;
        if (message is null || !message.Contains("exceeded quota", StringComparison.OrdinalIgnoreCase))
            return false;

        var resourceMatch = QuotaResourceRegex().Match(message);
        var exhaustedResource = resourceMatch.Success ? resourceMatch.Groups[1].Value : null;
        var text = $"Kubernetes ResourceQuota in namespace '{ns}' rejected the sandbox pod: {message}";
        mapped = inner is null
            ? new CapacityExhaustedException(ns, exhaustedResource, text)
            : new CapacityExhaustedException(ns, exhaustedResource, text, inner);
        return true;
    }

    // The k8s client puts the V1Status JSON in Response.Content. Deserialize for a clean
    // message; fall back to the raw content if that fails so the marker check still runs.
    private static string? ExtractStatusMessage(k8s.Autorest.HttpOperationException ex)
    {
        var content = ex.Response?.Content;
        if (string.IsNullOrEmpty(content)) return ex.Message;
        try
        {
            var status = KubernetesJson.Deserialize<V1Status>(content);
            return string.IsNullOrEmpty(status?.Message) ? content : status.Message;
        }
        catch (System.Text.Json.JsonException)
        {
            return content;
        }
    }

    [GeneratedRegex(@"requested:\s*([^=,\s]+)=", RegexOptions.IgnoreCase)]
    private static partial Regex QuotaResourceRegex();
}
