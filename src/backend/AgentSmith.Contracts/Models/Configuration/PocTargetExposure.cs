using System.Net;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-c81eb: the problems of how a PoC target exposes and confines its PoCs — ingress, auth and network. While
/// PoCs are on, a target names its ingress class, auth annotations (every PoC is behind auth), the ingress controller's
/// namespace and its egress proxy; values that are set are checked always, so the form may be filled in before the switch.
/// </summary>
public sealed class PocTargetExposure(PocTargetConfig target)
{
    public IEnumerable<string> Problems(bool pocsEnabled) =>
        [.. pocsEnabled ? Required() : [], .. ProxyProblems(pocsEnabled), .. DnsProblems()];

    private string Key(string field) => $"pocs.targets[].{field} of target '{target.Name}'";

    private IEnumerable<string> Required()
    {
        if (string.IsNullOrWhiteSpace(target.Ingress.Class))
            yield return $"{Key("ingress.class")} is empty while pocs.enabled is true; name the ingress class the target's "
                + "controller answers to, because every PoC is reached through an Ingress of that class";
        if (target.Auth.IngressAnnotations.Count == 0)
            yield return $"{Key("auth.ingress_annotations")} is empty while pocs.enabled is true; set the annotations that make "
                + "the ingress controller call the auth proxy, because every PoC is behind auth";
        if (string.IsNullOrWhiteSpace(target.Network.IngressControllerNamespace))
            yield return $"{Key("network.ingress_controller_namespace")} is empty while pocs.enabled is true; name the "
                + "namespace the ingress controller runs in, because each PoC's NetworkPolicy admits traffic from it alone";
        if (string.IsNullOrWhiteSpace(target.Network.EgressProxy.Url))
            yield return $"{Key("network.egress_proxy.url")} is empty while pocs.enabled is true; name the egress proxy, "
                + "because a PoC reaches the outside only through it";
    }

    private IEnumerable<string> ProxyProblems(bool pocsEnabled)
    {
        var proxy = target.Network.EgressProxy;
        if (!string.IsNullOrWhiteSpace(proxy.Url)
            && !(Uri.TryCreate(proxy.Url, UriKind.Absolute, out var url) && url.Scheme is "http" or "https"))
            yield return $"{Key("network.egress_proxy.url")} is '{proxy.Url}'; it must be an http or https URL, because "
                + "it is every PoC container's HTTP_PROXY and HTTPS_PROXY";
        if (proxy.Port is < 1 or > 65535)
            yield return $"{Key("network.egress_proxy.port")} is {proxy.Port}; it must be 1 to 65535, because the "
                + "NetworkPolicy opens that port of the proxy's pods";
        var cidr = !string.IsNullOrWhiteSpace(proxy.Cidr);
        if (proxy.BySelector == cidr && (cidr || pocsEnabled))
            yield return $"{Key("network.egress_proxy")} names the proxy {(cidr ? "both" : "neither")} by namespace and "
                + $"pod_labels {(cidr ? "and" : "nor")} by cidr; use exactly one form, because the NetworkPolicy's egress rule "
                + "takes one";
        if (proxy.BySelector && (string.IsNullOrWhiteSpace(proxy.Namespace) || proxy.PodLabels.Count == 0))
            yield return $"{Key("network.egress_proxy")} names a namespace or pod_labels without the other; give both, "
                + "because the NetworkPolicy selects the proxy's pods by namespace and labels together";
        if (cidr && !IPNetwork.TryParse(proxy.Cidr, out _))
            yield return $"{Key("network.egress_proxy.cidr")} is '{proxy.Cidr}'; it must be a CIDR such as 10.0.0.0/24, "
                + "because the NetworkPolicy's ipBlock takes one";
    }

    private IEnumerable<string> DnsProblems()
    {
        if (string.IsNullOrWhiteSpace(target.Network.DnsNamespace) || target.Network.DnsPodLabels.Count == 0)
            yield return $"{Key("network.dns_namespace")} or dns_pod_labels is empty; name the cluster DNS's namespace and "
                + "pod labels, because a PoC resolves names only through it";
    }
}
