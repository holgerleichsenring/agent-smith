using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Registry;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Registry;

/// <summary>
/// 2026-11-23-933de: the OCI distribution client — unsigned HEAD, Bearer challenge, realm checked before the pull
/// credentials go out, token, signed HEAD with OCI and Docker media types; no token, header or body in a log or refusal.
/// </summary>
public sealed class OciDistributionClientTests
{
    private const string Digest = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
    private readonly FakeRegistryHost _host = new();
    private readonly CapturingLogger<OciDistributionClientTests> _logger = new();

    [Fact]
    public async Task OciDistributionClient_ChallengeThenToken_ReadsDigestHeader()
    {
        _host.Push("pocs/shop", "abc123", Digest, OciMediaTypes.OciManifest);

        var head = await Client().HeadManifestAsync("pocs/shop", "abc123", CancellationToken.None);

        (head.State, head.Digest).Should().Be((OciManifestState.Found, Digest));
        _host.Requests.Select(r => (r.Method, r.Url.AbsolutePath)).Should().Equal(
            ("HEAD", "/v2/pocs/shop/manifests/abc123"), ("GET", "/oauth2/token"), ("HEAD", "/v2/pocs/shop/manifests/abc123"));
        _host.Requests[0].Auth.Should().BeEmpty("nothing is sent before the realm is checked");
        _host.Requests[1].Url.Query.Should().Contain("scope=repository%3Apocs%2Fshop%3Apull").And.Contain("service=");
    }

    [Fact]
    public async Task OciDistributionClient_Schema2Manifest_ResolvesToPushDigest()
    {
        _host.Push("pocs/shop", "abc123", Digest);

        var digest = await Registry().ResolveAsync(new ImageReference(FakeRegistryHost.LoginServer, "pocs/shop", "abc123"),
            CancellationToken.None);

        digest.Should().Be(Digest);
        _host.Requests[^1].Accept.Should().Contain(OciMediaTypes.DockerManifest).And.Contain(OciMediaTypes.DockerManifestList)
            .And.Contain(OciMediaTypes.OciIndex).And.Contain(OciMediaTypes.OciManifest);
    }

    [Fact]
    public async Task OciDistributionClient_UnknownManifest_AnswersNull()
    {
        var digest = await Registry().ResolveAsync(new ImageReference(FakeRegistryHost.LoginServer, "pocs/shop", "nope"),
            CancellationToken.None);

        digest.Should().BeNull();
    }

    [Fact]
    public async Task OciDistributionClient_RealmOnForeignHost_RefusedBeforeCredentialsSent()
    {
        _host.Realm = "https://collector.example.net/oauth2/token";

        var head = () => Client().HeadManifestAsync("pocs/shop", "abc123", CancellationToken.None);

        (await head.Should().ThrowAsync<ContainerRegistryException>()).Which.Message.Should()
            .Contain("collector.example.net").And.Contain(FakeRegistryHost.LoginServer);
        _host.Requests.Should().ContainSingle().Which.Auth.Should().BeEmpty();
        _host.Requests.Should().NotContain(r => r.Url.Host == "collector.example.net");
    }

    [Fact]
    public async Task OciDistributionClient_PlainHttpRealmOnTheLoginServer_Refused()
    {
        _host.Realm = $"http://{FakeRegistryHost.LoginServer}/oauth2/token";

        var head = () => Client().HeadManifestAsync("pocs/shop", "abc123", CancellationToken.None);

        await head.Should().ThrowAsync<ContainerRegistryException>();
        _host.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task OciDistributionClient_Refusal_CarriesNoTokenHeaderOrBody()
    {
        _host.ScopeMap = "elsewhere/";

        var exists = () => Registry().ExistsAsync(
            new ImageReference(FakeRegistryHost.LoginServer, "pocs/shop", Digest: Digest), CancellationToken.None);

        var refusal = (await exists.Should().ThrowAsync<ContainerRegistryException>()).Which.Message;
        refusal.Should().Contain(FakeRegistryHost.LoginServer).And.Contain("pocs/shop").And.Contain("401");
        var basic = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{FakeRegistryHost.PullUser}:{FakeRegistryHost.PullToken}"));
        foreach (var text in _logger.Lines.Append(refusal))
            text.Should().NotContain(FakeRegistryHost.Bearer).And.NotContain(FakeRegistryHost.PullToken).And.NotContain(basic)
                .And.NotContain("scope ");
    }

    [Fact]
    public async Task OciDistributionClient_TokenRefused_SaysSoWithoutTheToken()
    {
        _host.RefusesToken = true;

        var head = await Client().HeadManifestAsync("pocs/shop", "abc123", CancellationToken.None);

        head.State.Should().Be(OciManifestState.TokenRefused);
        _logger.Lines.Should().NotContain(l => l.Contains(FakeRegistryHost.PullToken) || l.Contains("denied"));
    }

    private OciDistributionClient Client() =>
        new(_host.Client(), FakeRegistryHost.LoginServer, FakeRegistryHost.PullUser, FakeRegistryHost.PullToken, _logger);

    private AcrContainerRegistry Registry() =>
        new(PocsConfigTests.SoundRegistry(), new OciRegistryReader(Client(), "pocs"));
}
