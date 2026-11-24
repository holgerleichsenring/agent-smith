using AgentSmith.Contracts.Pocs;
using YamlDotNet.RepresentationModel;
using static AgentSmith.Application.Services.Pocs.GitOps.PocYamlTree;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: sets container 'app' — the image by digest; readiness by httpGet on the PoC's path or tcpSocket on
/// the port; envFrom poc-config and, only when the PoC has a secret variable, poc-secrets (never optional, so a pod
/// never starts without its secrets); and the proxy variables directly on the container, so a proxy change restarts it.
/// </summary>
public sealed class PocContainerFields
{
    public string? Apply(YamlMappingNode container, PocRenderInput input)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(input);
        Set(container, "image", PocYamlText.Text(input.Image));
        Set(container, "readinessProbe", Probe(input));
        var from = new YamlSequenceNode(Ref("configMapRef", PocSecretNames.Config));
        if (input.Seal.HasSecrets) from.Add(Ref("secretRef", PocSecretNames.Secrets));
        Set(container, "envFrom", from);
        Proxy(container, input.Target.Network.EgressProxy.Url ?? string.Empty, input.Target.Network.EgressProxy.NoProxy);
        return null;
    }

    private static YamlMappingNode Probe(PocRenderInput input) => input.Desired.ReadinessPath is { Length: > 0 } path
        ? new YamlMappingNode { { "httpGet", new YamlMappingNode { { "path", PocYamlText.Text(path) }, { "port", PocYamlText.Number(input.Port) } } } }
        : new YamlMappingNode { { "tcpSocket", new YamlMappingNode { { "port", PocYamlText.Number(input.Port) } } } };

    private static YamlMappingNode Ref(string kind, string name) =>
        new() { { kind, new YamlMappingNode { { "name", PocYamlText.Text(name) } } } };

    private static void Proxy(YamlMappingNode container, string url, string noProxy)
    {
        var values = new (string Name, string Value)[]
        {
            ("HTTP_PROXY", url), ("HTTPS_PROXY", url), ("http_proxy", url), ("https_proxy", url),
            ("NO_PROXY", noProxy), ("no_proxy", noProxy),
        };
        var names = values.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);
        var env = new YamlSequenceNode(Items(container, "env").Where(e => !names.Contains(Text(e, "name") ?? string.Empty)));
        foreach (var (name, value) in values)
            env.Add(new YamlMappingNode { { "name", PocYamlText.Text(name) }, { "value", PocYamlText.Text(value) } });
        Set(container, "env", env);
    }
}
