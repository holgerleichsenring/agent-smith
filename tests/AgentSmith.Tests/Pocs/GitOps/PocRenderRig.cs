using AgentSmith.Application.Services.Pocs.GitOps;
using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: a render over the real default template, the sound target and a seal of the caller's
/// choosing.</summary>
internal static class PocRenderRig
{
    public const string Digest = "sha256:" + "c81eb000000000000000000000000000000000000000000000000000000000ab";

    public static PocTargetConfig Target() => PocsConfigTests.Sound().Targets[0];

    public static PocAppRender Render() =>
        new(new PocDeploymentFields(new PocContainerFields()), new PocIngressFields(), new PocConfigMapRender(),
            new PocManifestPolicy(new PocPodPolicy()));

    public static PocTemplate DefaultTemplate(PocTargetConfig? target = null) =>
        new(new PocAppTemplateDefaults(new PocYamlFill()).Render(target ?? Target()), []);

    public static PocSealResult Seal(bool secrets = false, bool unchanged = false) =>
        new(unchanged, unchanged ? [] : Files(secrets), "hmac-sha256:" + new string('a', 64), "checksum-1")
        {
            HasSecrets = secrets,
            NeededFiles = secrets ? ["poc-secrets.sealed.yaml", "poc-pull.sealed.yaml"] : ["poc-pull.sealed.yaml"],
        };

    private static List<PocSecretFile> Files(bool secrets) =>
        [.. secrets ? [new PocSecretFile("poc-secrets.sealed.yaml", "kind: SealedSecret # secrets\n")] : Array.Empty<PocSecretFile>(),
            new PocSecretFile("poc-pull.sealed.yaml", "kind: SealedSecret # pull\n")];

    public static PocRenderInput Input(
        string name = "shop", int replicas = 1, string? readiness = null, PocTargetConfig? target = null, PocSealResult? seal = null,
        IReadOnlyList<PocPlainVariable>? plain = null, int port = 8080) =>
        new(name, PocNamespace.For(name), $"{name}.poc.example.internal", port, $"pocregistry.azurecr.io/pocs/{name}:abc@{Digest}",
            new PocDesiredState(replicas, 2, Digest, readiness), target ?? Target(), seal ?? Seal(), plain ?? []);

    public static IReadOnlyList<PocRenderedFile> Files(PocRenderInput input, PocTemplate? template = null)
    {
        var (files, refused) = Render().Render(template ?? DefaultTemplate(input.Target), input);
        return files ?? throw new InvalidOperationException(refused);
    }

    public static string File(IReadOnlyList<PocRenderedFile> files, string name) => files.Single(f => f.Name == name).Content;
}
