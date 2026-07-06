using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Exceptions;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

public sealed partial class KubernetesSandboxFactory(
    IKubernetes client,
    IConnectionMultiplexer redis,
    PodSpecBuilder podSpecBuilder,
    KubernetesSandboxOptions options,
    IOptions<SandboxGlobalConfig> sandboxConfig,
    ILoggerFactory loggerFactory) : ISandboxFactory
{
    public async Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var podName = $"agentsmith-sandbox-{jobId[..12]}";
        var pod = podSpecBuilder.Build(podName, jobId, options.RedisUrl, spec, options.OwnerReference);

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
        loggerFactory.CreateLogger<KubernetesSandboxFactory>()
            .LogInformation("Sandbox pod {Pod} created in namespace {Ns}", podName, options.Namespace);

        var watcher = new KubernetesPodWatcher(client, loggerFactory.CreateLogger<KubernetesPodWatcher>());
        await watcher.WaitForReadyAsync(podName, options.Namespace,
            TimeSpan.FromSeconds(spec.TimeoutSeconds), cancellationToken);

        var channel = new SandboxRedisChannel(redis, jobId, loggerFactory.CreateLogger<SandboxRedisChannel>());
        return new KubernetesSandbox(client, options.Namespace, podName, jobId, channel,
            // p0230: per-project resolved cap rides on the spec; fall back to global.
            spec.StepTimeoutSeconds ?? sandboxConfig.Value.StepTimeoutSeconds,
            loggerFactory.CreateLogger<KubernetesSandbox>());
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
        catch
        {
            return content;
        }
    }

    [GeneratedRegex(@"requested:\s*([^=,\s]+)=", RegexOptions.IgnoreCase)]
    private static partial Regex QuotaResourceRegex();
}
