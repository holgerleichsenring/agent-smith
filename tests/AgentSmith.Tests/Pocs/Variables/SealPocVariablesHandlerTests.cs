using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Sealing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using AgentSmith.Infrastructure.Services.Pocs.Sealing;
using AgentSmith.Tests.Pocs.Sealing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: SealPocVariables over the real store and sealer — what it renders per pull mode and
/// variables, the fingerprint that settles Unchanged, and every refusal before anything is sealed.</summary>
public sealed class SealPocVariablesHandlerTests : IDisposable
{
    private const string Token = "pull-token-value-1";
    private readonly SecretStoreRig _rig = new();
    private readonly RSA _key = RSA.Create(2048);
    private readonly PocTargetConfig _target;
    private string _token = Token;

    public SealPocVariablesHandlerTests()
    {
        var request = new CertificateRequest("CN=sealed-secret", _key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        _target = new PocTargetConfig
        {
            Name = "main", SourceRepo = "poc-src", Registry = PocsConfigTests.SoundRegistry(),
            Secrets = new PocSecretsConfig { SealedSecretsCert = cert.ExportCertificatePem() },
        };
    }

    [Fact]
    public async Task Seal_PullSecret_RendersADockerConfigJsonPocPull_ForTheLoginServer_UnderItsEntryKey()
    {
        var poc = await _rig.PocAsync("todo", "poc-ann");

        var seal = await SealAsync(poc);

        var pull = seal.Files.Should().ContainSingle().Which;
        pull.FileName.Should().Be("poc-pull.sealed.yaml");
        pull.Content.Should().Contain("kubernetes.io/dockerconfigjson").And.Contain("namespace: poc-todo").And.NotContain(Token);
        var json = Open(pull.Content, PocSecretNames.DockerConfigJsonKey, "poc-todo", PocSecretNames.Pull);
        json.Should().Contain("\"pocregistry.azurecr.io\"").And.Contain("\"username\":\"poc-pull\"").And.Contain(Token);
    }

    [Fact]
    public async Task Seal_ClusterPulls_RendersNone_AndNoSecretVariableRendersNoPocSecrets()
    {
        _target.Registry!.PullMode = PocRegistryConfig.ClusterPullsMode;
        var poc = await _rig.PocAsync("todo", "poc-ann");

        var seal = await SealAsync(poc);

        (seal.Files.Count, seal.SealFingerprint, seal.Unchanged).Should().Be((0, null, true));
        seal.VariablesChecksum.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task Seal_ASecretVariable_SealsItIntoPocSecrets_ForTheNamespace()
    {
        var poc = await _rig.PocAsync("todo", "poc-ann");
        await _rig.SetAsync(SecretScope.Poc(poc.Id), "OPENAI_API_KEY", "sk-live-value-933dg");

        var seal = await SealAsync(poc);

        var secrets = seal.Files.Single(f => f.FileName == "poc-secrets.sealed.yaml");
        secrets.Content.Should().Contain("type: Opaque").And.NotContain("sk-live-value-933dg");
        Open(secrets.Content, "OPENAI_API_KEY", "poc-todo", PocSecretNames.Secrets).Should().Be("sk-live-value-933dg");
    }

    [Fact]
    public async Task Seal_AnEqualFingerprint_AnswersUnchanged_ARotatedTokenChangesIt()
    {
        var poc = await _rig.PocAsync("todo", "poc-ann");
        var first = await SealAsync(poc);
        await RecordAsync(poc.Id, first.SealFingerprint);

        var again = await SealAsync(poc);
        _token = "pull-token-value-2";
        var rotated = await SealAsync(poc);

        (again.Unchanged, again.Files.Count, again.VariablesChecksum).Should().Be((true, 0, first.VariablesChecksum));
        (rotated.Unchanged, rotated.SealFingerprint).Should().NotBe((true, first.SealFingerprint));
    }

    [Fact]
    public async Task Seal_NoCertificate_IsRefusedNamingTheKey()
    {
        _target.Secrets.SealedSecretsCert = null;
        var poc = await _rig.PocAsync("todo", "poc-ann");

        (await RunAsync(poc)).Message.Should().Contain("pocs.targets[].secrets.sealed_secrets_cert of target 'main' is empty");
    }

    [Fact]
    public async Task Seal_PullSecretWithoutAMasterKey_IsRefusedNamingPullModeAndTheVariable_NothingSealed()
    {
        var poc = await _rig.PocAsync("todo", "poc-ann");
        var context = Context(poc);

        var result = await Handler(new PocSealFingerprint(new MasterKeyRing(_ => null))).ExecuteAsync(context, default);

        result.Message.Should().Contain("pocs.targets[].registry.pull_mode").And.Contain("AGENTSMITH_SECRET_MASTER_KEY");
        context.Pipeline.Has(ContextKeys.PocSeal).Should().BeFalse();
    }

    [Fact]
    public async Task Seal_AnUnreadableVariable_IsRefusedByName_NothingSealed()
    {
        var poc = await _rig.PocAsync("todo", "poc-ann");
        await _rig.SetAsync(SecretScope.Poc(poc.Id), "OPENAI_API_KEY", "sk-live-value-933dg");
        await using (var db = _rig.Db()) await db.Database.ExecuteSqlRawAsync("UPDATE StoredSecrets SET Ciphertext = x'00112233445566778899aabbccddeeff00'");
        var context = Context(poc);

        var result = await Handler().ExecuteAsync(context, default);

        result.Message.Should().StartWith("PoC variable OPENAI_API_KEY cannot be read").And.EndWith("set it again.");
        context.Pipeline.Has(ContextKeys.PocSeal).Should().BeFalse();
    }

    private async Task<PocSealResult> SealAsync(Poc poc)
    {
        var context = Context(poc);
        var result = await Handler().ExecuteAsync(context, default);
        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("nothing is committed before the GitOps commit step").And.NotContain(_token);
        return context.Pipeline.Get<PocSealResult>(ContextKeys.PocSeal);
    }

    private Task<AgentSmith.Domain.Models.CommandResult> RunAsync(Poc poc) => Handler().ExecuteAsync(Context(poc), default);

    private SealPocVariablesHandler Handler(IPocSealFingerprint? fingerprint = null)
    {
        var config = new AgentSmithConfig { Pocs = new PocsConfig { Targets = [_target] } };
        var source = new PocsConfigSource(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config), new ServerContext("c.yml"));
        var credentials = new Mock<ICredentialResolver>();
        credentials.Setup(c => c.Lookup("poc_pull_token")).Returns(() => CredentialLookup.Of(_token));
        var print = fingerprint ?? _rig.Fingerprint();
        var provider = new SealedSecretsProvider(new KubeSealer(new EmeOaep(), new RsaPublicOperation()), new KubeSealedSecretManifest());
        return new SealPocVariablesHandler(_rig.Variables(), source, new PocSealPlanner([provider], _rig.PocValues(), credentials.Object, print),
            new PocSealer(print), NullLogger<SealPocVariablesHandler>.Instance);
    }

    private static SealPocVariablesContext Context(Poc poc)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PocId, poc.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new SealPocVariablesContext(pipeline, new ResolvedProject { Name = poc.Project }, PocSeeds.From(pipeline));
    }

    private async Task RecordAsync(long pocId, string? fingerprint)
    {
        await using var db = _rig.Db();
        await db.Set<Poc>().Where(p => p.Id == pocId).ExecuteUpdateAsync(s => s.SetProperty(p => p.SealFingerprint, fingerprint));
    }

    private string Open(string manifest, string key, string ns, string name)
    {
        var value = Regex.Match(manifest, $"\"{Regex.Escape(key)}\": (\\S+)").Groups[1].Value;
        return Encoding.UTF8.GetString(KubeOpener.Open(_key.ExportParameters(true), Convert.FromBase64String(value), KubeSealer.Label(ns, name)));
    }

    public void Dispose()
    {
        _key.Dispose();
        _rig.Dispose();
    }
}
