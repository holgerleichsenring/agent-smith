using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using System.Text.RegularExpressions;
using FluentAssertions;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: the render of the default template — typed fields, hardening, quoting, scope and the
/// fields agent smith owns.</summary>
public sealed class PocAppRenderTests
{
    private static string Deployment(PocRenderInput input) => PocRenderRig.File(PocRenderRig.Files(input), "deployment.yaml");

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Render_Replicas_IsAPlainInteger(int replicas) =>
        Deployment(PocRenderRig.Input(replicas: replicas)).Should().Contain($"  replicas: {replicas}\n").And.NotContain("replicas: \"");

    [Fact]
    public void Render_ATemplateWithQuotedReplicas_StillWritesAPlainInteger()
    {
        var template = PocRenderRig.DefaultTemplate();
        var quoted = new PocTemplate(template.Files.Select(f => f.Name == "deployment.yaml"
            ? f with { Text = f.Text.Replace("replicas: 1", "replicas: \"1\"") } : f).ToList(), []);

        PocRenderRig.File(PocRenderRig.Files(PocRenderRig.Input(replicas: 0), quoted), "deployment.yaml").Should().Contain("  replicas: 0\n");
    }

    [Fact]
    public void Render_Hardened()
    {
        var deployment = Deployment(PocRenderRig.Input());

        deployment.Should().Contain("runAsUser: 10001").And.Contain("readOnlyRootFilesystem: true").And.Contain("drop: [\"ALL\"]")
            .And.Contain("allowPrivilegeEscalation: false").And.Contain("mountPath: /tmp").And.Contain("emptyDir:")
            .And.Contain("limits:").And.Contain("automountServiceAccountToken: false");
    }

    [Fact]
    public void Render_PullSecretMode_ImagePullSecrets() =>
        Deployment(PocRenderRig.Input()).Should().Contain("imagePullSecrets:\n      - name: poc-pull");

    [Fact]
    public void Render_ClusterPulls_None()
    {
        var target = PocRenderRig.Target();
        target.Registry!.PullMode = PocRegistryConfig.ClusterPullsMode;

        Deployment(PocRenderRig.Input(target: target)).Should().NotContain("imagePullSecrets");
    }

    [Fact]
    public void Render_ReadinessPathNull_TcpSocket() =>
        Deployment(PocRenderRig.Input()).Should().Contain("readinessProbe:\n          tcpSocket:\n            port: 8080");

    [Fact]
    public void Render_ReadinessPath_HttpGet() =>
        Deployment(PocRenderRig.Input(readiness: "/health")).Should().Contain("httpGet:\n            path: /health\n            port: 8080");

    [Fact]
    public void Render_AuthAnnotationsOnIngress()
    {
        var ingress = PocRenderRig.File(PocRenderRig.Files(PocRenderRig.Input()), "ingress.yaml");

        ingress.Should().Contain("nginx.ingress.kubernetes.io/auth-url: https://auth.poc.example.internal/oauth2/auth")
            .And.Contain("ingressClassName: nginx").And.Contain("host: shop.poc.example.internal");
    }

    [Fact]
    public void Render_TlsIssuer_SetsTlsAndTheIssuer()
    {
        var target = PocRenderRig.Target();
        target.Ingress.TlsIssuer = "letsencrypt";

        PocRenderRig.File(PocRenderRig.Files(PocRenderRig.Input(target: target)), "ingress.yaml")
            .Should().Contain("cert-manager.io/cluster-issuer: letsencrypt").And.Contain("secretName: app-tls");
    }

    [Fact]
    public void Render_ImageFromImageReferenceWithDigest() =>
        Deployment(PocRenderRig.Input()).Should().Contain($"image: pocregistry.azurecr.io/pocs/shop:abc@{PocRenderRig.Digest}");

    [Fact]
    public void Render_NamespaceFromPocNamespace()
    {
        var files = PocRenderRig.Files(PocRenderRig.Input());

        PocRenderRig.File(files, "namespace.yaml").Should().MatchRegex("name: \"?poc-shop\"?\n");
        files.Where(f => f.Name != "namespace.yaml").Should().OnlyContain(f => Regex.IsMatch(f.Content, "namespace: \"?poc-shop\"?\n"));
    }

    [Fact]
    public void Render_SameInput_SameBytes() =>
        PocRenderRig.Files(PocRenderRig.Input()).Should().BeEquivalentTo(PocRenderRig.Files(PocRenderRig.Input()), o => o.WithStrictOrdering());

    [Fact]
    public void Render_ChecksumAnnotation_IsTheSealsVariablesChecksum() =>
        Deployment(PocRenderRig.Input()).Should().Contain("agent-smith.io/variables-checksum: checksum-1");

    [Fact]
    public void Render_AProxyChange_ChangesThePodTemplate()
    {
        var other = PocRenderRig.Target();
        other.Network.EgressProxy.Url = "http://proxy.other:8080";

        Deployment(PocRenderRig.Input(target: other)).Should().NotBe(Deployment(PocRenderRig.Input()));
        Deployment(PocRenderRig.Input(target: other)).Should().Contain("value: http://proxy.other:8080");
    }

    [Fact]
    public void Render_ProxyVariables_BothCasesSet_AndNoProxyDefaultHoldsLocalhost()
    {
        var deployment = Deployment(PocRenderRig.Input());

        foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy", "NO_PROXY", "no_proxy" })
            deployment.Should().Contain($"name: {name}");
        deployment.Should().Contain("localhost,127.0.0.1,.svc,.cluster.local");
    }

    [Fact]
    public void Render_Quoting_NonStringsRenderDoubleQuoted()
    {
        var target = PocRenderRig.Target();
        target.Auth.IngressAnnotations["nginx.ingress.kubernetes.io/ssl-redirect"] = "true";
        var plain = new List<PocPlainVariable> { new("DEBUG", "true", "ann", DateTimeOffset.UnixEpoch), new("PORT", "8080", "ann", DateTimeOffset.UnixEpoch) };

        var files = PocRenderRig.Files(PocRenderRig.Input(name: "123", target: target, plain: plain));

        PocRenderRig.File(files, "configmap.yaml").Should().Contain("DEBUG: \"true\"").And.Contain("PORT: \"8080\"");
        PocRenderRig.File(files, "ingress.yaml").Should().Contain("ssl-redirect: \"true\"");
        PocRenderRig.File(files, "deployment.yaml").Should().Contain("agent-smith.io/poc: \"123\"").And.NotContain("agent-smith.io/poc: 123");
        PocRenderRig.File(files, "service.yaml").Should().Contain("agent-smith.io/poc: \"123\"");
    }

    [Fact]
    public void Render_ASecretVariable_SecretRefWithoutOptional_AndTheWaveOnTheDeploymentOnly()
    {
        var files = PocRenderRig.Files(PocRenderRig.Input(seal: PocRenderRig.Seal(secrets: true)));
        var deployment = PocRenderRig.File(files, "deployment.yaml");

        deployment.Should().Contain("secretRef:\n            name: poc-secrets").And.NotContain("optional");
        deployment.Should().Contain("argocd.argoproj.io/sync-wave: \"1\"");
        files.Where(f => f.Name != "deployment.yaml").Should().OnlyContain(f => !f.Content.Contains("sync-wave"));
    }

    [Fact]
    public void Render_NoSecretVariable_NoSecretRef() =>
        Deployment(PocRenderRig.Input()).Should().Contain("configMapRef:").And.NotContain("secretRef");

    [Fact]
    public void Render_ANonYamlFile_IsSkipped_AndATwoDocumentFile_RendersBoth()
    {
        var template = PocRenderRig.DefaultTemplate();
        var extra = "apiVersion: v1\nkind: ServiceAccount\nmetadata:\n  name: one\n  namespace: \"{namespace}\"\n---\n"
            + "apiVersion: v1\nkind: ServiceAccount\nmetadata:\n  name: two\n  namespace: \"{namespace}\"\n";
        var files = PocRenderRig.Files(PocRenderRig.Input(), new PocTemplate([.. template.Files, new("extra.yaml", extra)], ["README.md"]));

        files.Should().NotContain(f => f.Name == "README.md");
        PocRenderRig.File(files, "extra.yaml").Should().Contain("name: one").And.Contain("---\n").And.Contain("name: two");
    }

    [Fact]
    public void Render_TheRollbackRevisionsPort_IsRendered() =>
        PocRenderRig.File(PocRenderRig.Files(PocRenderRig.Input(port: 5000)), "service.yaml").Should().Contain("targetPort: 5000");

    [Fact]
    public void Render_ServiceSelectorAndNetworkPolicyPodSelector_EqualThePodTemplateLabels()
    {
        var files = PocRenderRig.Files(PocRenderRig.Input());
        var pod = Labels(PocRenderRig.File(files, "deployment.yaml"), "spec", "template", "metadata", "labels");

        Labels(PocRenderRig.File(files, "service.yaml"), "spec", "selector").Should().BeEquivalentTo(pod);
        Labels(PocRenderRig.File(files, "networkpolicy.yaml"), "spec", "podSelector", "matchLabels").Should().BeEquivalentTo(pod);
    }

    private static Dictionary<string, string> Labels(string yaml, params string[] path)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var map = PocYamlTree.MapOf((YamlMappingNode)stream.Documents[0].RootNode, path)!;
        return map.Children.ToDictionary(c => ((YamlScalarNode)c.Key).Value!, c => ((YamlScalarNode)c.Value).Value!);
    }
}
