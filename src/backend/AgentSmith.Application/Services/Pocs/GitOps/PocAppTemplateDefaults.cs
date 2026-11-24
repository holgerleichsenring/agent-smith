using System.Globalization;
using System.Text.Json;
using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: the default app template a target's install writes — six manifests embedded from
/// Resources/PocAppTemplate, filled at install with the target's quota, container resources and network keys (every
/// value a JSON-quoted string or a number, so it reads as written). The render-time placeholders ({app}, {namespace},
/// {host}, {port}) stay in the files; once written, the files are the operator's.
/// </summary>
public sealed class PocAppTemplateDefaults(PocYamlFill fill)
{
    public static readonly IReadOnlyList<string> Names =
        ["namespace.yaml", "resourcequota.yaml", "deployment.yaml", "service.yaml", "ingress.yaml", "networkpolicy.yaml"];

    private const string Prefix = "AgentSmith.Application.Resources.PocAppTemplate.";

    public IReadOnlyList<PocTemplateFile> Render(PocTargetConfig target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var scalars = Scalars(target);
        var blocks = new Dictionary<string, string> { ["EGRESS_PROXY_TO"] = ProxyTo(target.Network.EgressProxy) };
        return Names.Select(name => new PocTemplateFile(name, fill.Fill(Text(name), scalars, blocks))).ToList();
    }

    private static Dictionary<string, string> Scalars(PocTargetConfig target)
    {
        var (quota, container, network) = (target.Quota, target.Container, target.Network);
        return new Dictionary<string, string>
        {
            ["MARKER"] = $"agent-smith: poc app template of target {target.Name}, written once by poc-target-install; yours to edit "
                + "(the hardening must stay)",
            ["QUOTA_CPU"] = Json(quota.Cpu), ["QUOTA_MEMORY"] = Json(quota.Memory),
            ["QUOTA_EPHEMERAL_STORAGE"] = Json(quota.EphemeralStorage), ["QUOTA_PODS"] = Json(quota.Pods.ToString(CultureInfo.InvariantCulture)),
            ["CPU_REQUEST"] = Json(container.CpuRequest), ["MEMORY_REQUEST"] = Json(container.MemoryRequest),
            ["CPU_LIMIT"] = Json(container.CpuLimit), ["MEMORY_LIMIT"] = Json(container.MemoryLimit),
            ["EPHEMERAL_LIMIT"] = Json(container.EphemeralLimit),
            ["INGRESS_NAMESPACE"] = Json(network.IngressControllerNamespace ?? string.Empty),
            ["DNS_NAMESPACE"] = Json(network.DnsNamespace), ["DNS_POD_LABELS"] = Labels(network.DnsPodLabels),
            ["EGRESS_PROXY_PORT"] = network.EgressProxy.Port.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static string ProxyTo(PocEgressProxyConfig proxy) => proxy.BySelector
        ? $"- namespaceSelector:\n    matchLabels:\n      kubernetes.io/metadata.name: {Json(proxy.Namespace ?? string.Empty)}\n"
          + $"  podSelector:\n    matchLabels: {Labels(proxy.PodLabels)}"
        : $"- ipBlock:\n    cidr: {Json(proxy.Cidr ?? string.Empty)}";

    private static string Labels(IReadOnlyDictionary<string, string> labels) =>
        "{" + string.Join(", ", labels.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => $"{Json(l.Key)}: {Json(l.Value)}")) + "}";

    private static string Json(string value) => JsonSerializer.Serialize(value);

    private static string Text(string name)
    {
        using var stream = typeof(PocAppTemplateDefaults).Assembly.GetManifestResourceStream(Prefix + name)
            ?? throw new InvalidOperationException($"The app template file '{name}' is not embedded");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
