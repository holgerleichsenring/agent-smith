using AgentSmith.Application.Services.Pocs.GitOps;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: an operator template that lost the hardening is refused naming the file and the field;
/// a render without a seal is refused naming the seal step.</summary>
public sealed class PocManifestPolicyTests
{
    private static string? Refusal(Func<string, string> edit)
    {
        var template = PocRenderRig.DefaultTemplate();
        var edited = new PocTemplate(template.Files.Select(f => f.Name == "deployment.yaml" ? f with { Text = edit(f.Text) } : f).ToList(), []);
        return PocRenderRig.Render().Render(edited, PocRenderRig.Input()).Refusal;
    }

    [Fact]
    public void Check_ATemplateWithoutRunAsNonRoot_IsRefusedNamingTheFileAndField() =>
        Refusal(t => t.Replace("          securityContext:\n            runAsNonRoot: true\n", "          securityContext:\n"))
            .Should().Contain("deployment.yaml").And.Contain("securityContext.runAsNonRoot");

    [Fact]
    public void Check_ASecondContainerWithoutTheHardening_IsRefused() =>
        Refusal(t => t.Replace("      volumes:", "        - name: side\n          image: busybox\n      volumes:"))
            .Should().Contain("containers[side]").And.Contain("readOnlyRootFilesystem");

    [Fact]
    public void Check_AnInitContainerWithoutTheHardening_IsRefused() =>
        Refusal(t => t.Replace("      containers:", "      initContainers:\n        - name: init\n          image: busybox\n      containers:"))
            .Should().Contain("initContainers[init]");

    [Fact]
    public void Check_HostNetwork_IsRefused() =>
        Refusal(t => t.Replace("      automountServiceAccountToken: false", "      automountServiceAccountToken: false\n      hostNetwork: true"))
            .Should().Contain("spec.hostNetwork");

    [Fact]
    public void Check_AHostPathVolume_IsRefused() =>
        Refusal(t => t + "        - name: node\n          hostPath:\n            path: /etc\n").Should().Contain("hostPath");

    [Fact]
    public void Check_AResourceOutsideTheNamespace_IsRefused() =>
        Refusal(t => t.Replace("  namespace: \"{namespace}\"", "  namespace: kube-system")).Should().Contain("metadata.namespace");

    [Fact]
    public void Check_NoNetworkPolicy_IsRefused()
    {
        var template = PocRenderRig.DefaultTemplate();
        var without = new PocTemplate(template.Files.Where(f => f.Name != "networkpolicy.yaml").ToList(), []);

        PocRenderRig.Render().Render(without, PocRenderRig.Input()).Refusal.Should().Contain("no NetworkPolicy");
    }
}
