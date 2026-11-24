using YamlDotNet.RepresentationModel;
using static AgentSmith.Application.Services.Pocs.GitOps.PocYamlTree;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: sets the Ingress 'app' — the target's ingress class, the PoC's host on every rule, every auth
/// annotation (every PoC is behind auth) and, with ingress.tls_issuer, TLS secret 'app-tls' signed by that cert-manager
/// cluster issuer. Without an issuer the template's own tls block stays as it is.
/// </summary>
public sealed class PocIngressFields
{
    public string? Apply(YamlMappingNode ingress, PocRenderInput input)
    {
        ArgumentNullException.ThrowIfNull(ingress);
        ArgumentNullException.ThrowIfNull(input);
        var spec = Map(ingress, "spec");
        var rules = Items(spec, "rules").ToList();
        if (rules.Count == 0) return $"the Ingress '{PocManifestNames.App}' has no rule to set the host on (spec.rules)";
        Set(spec, "ingressClassName", PocYamlText.Text(input.Target.Ingress.Class ?? string.Empty));
        foreach (var rule in rules) Set(rule, "host", PocYamlText.Text(input.Host));
        var annotations = Map(Map(ingress, "metadata"), "annotations");
        foreach (var (name, value) in input.Target.Auth.IngressAnnotations.OrderBy(a => a.Key, StringComparer.Ordinal))
            Set(annotations, name, PocYamlText.Text(value));
        if (input.Target.Ingress.TlsIssuer is not { Length: > 0 } issuer) return null;
        Set(annotations, PocManifestNames.IssuerAnnotation, PocYamlText.Text(issuer));
        Set(spec, "tls", new YamlSequenceNode(new YamlMappingNode
        {
            { "hosts", new YamlSequenceNode(PocYamlText.Text(input.Host)) },
            { "secretName", PocYamlText.Text(PocManifestNames.TlsSecret) },
        }));
        return null;
    }
}
