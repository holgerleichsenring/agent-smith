using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Registry;

/// <summary>
/// 2026-11-23-933de: ACR through the factory — the probe tells a reachable prefix from a scope map that misses it, a
/// reference is login server + repository + PoC, a digest exists or not, and the pull token is looked up environment
/// first, a missing or unreadable one refused.
/// </summary>
public sealed class AcrContainerRegistryTests
{
    private const string Digest = "sha256:2222222222222222222222222222222222222222222222222222222222222222";
    private const string OtherDigest = "sha256:3333333333333333333333333333333333333333333333333333333333333333";
    private readonly FakeRegistryHost _host = new();
    private Dictionary<string, string> _secrets = new() { ["poc_pull_token"] = FakeRegistryHost.PullToken };
    private readonly Dictionary<string, string> _unreadable = [];
    private readonly Dictionary<string, string> _environment = [];

    [Fact]
    public async Task AcrContainerRegistry_Probe_404Reaches_403ScopeMapMisses()
    {
        var reached = await Registry().ProbeAsync(CancellationToken.None);
        reached.Outcome.Should().Be(RegistryProbeOutcome.Reached);
        reached.Sentence.Should().Contain("reaches pocregistry.azurecr.io/pocs");
        _host.Requests[^1].Url.AbsolutePath.Should().Be("/v2/pocs/agent-smith-probe/manifests/probe");

        _host.ScopeMap = "other/";
        var missing = await Registry().ProbeAsync(CancellationToken.None);
        missing.Outcome.Should().Be(RegistryProbeOutcome.ScopeMissing);
        missing.Sentence.Should().Contain("may not pull pocs/*").And.Contain("scope map");

        _host.RefusesToken = true;
        (await Registry().ProbeAsync(CancellationToken.None)).Sentence.Should().Be("The registry refused the pull token.");
    }

    [Fact]
    public void AcrContainerRegistry_Reference_UsesLoginServerRepositoryAndPoc() =>
        Registry().Reference("shop", "abc123").ToString().Should().Be("pocregistry.azurecr.io/pocs/shop:abc123");

    [Fact]
    public async Task AcrContainerRegistry_ExistsByDigest_TrueAndFalse()
    {
        _host.Push("pocs/shop", "abc123", Digest);
        var registry = Registry();
        var reference = registry.Reference("shop", "abc123");

        (await registry.ExistsAsync(reference.WithDigest(Digest), CancellationToken.None)).Should().BeTrue();
        (await registry.ExistsAsync(reference.WithDigest(OtherDigest), CancellationToken.None)).Should().BeFalse();
        await registry.Invoking(r => r.ExistsAsync(reference, CancellationToken.None)).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void OciDistributionClient_PullAuthMissing_RefusedNamingKey()
    {
        _secrets = [];

        var create = () => Factory().Create(PocsConfigTests.SoundRegistry());

        create.Should().Throw<ContainerRegistryException>().WithMessage("pocs.targets[].registry.pull_auth names 'poc_pull_token'*");
    }

    [Fact]
    public void OciDistributionClient_PullAuthUnreadable_RefusedWithReason()
    {
        _secrets = new() { ["poc_pull_token"] = "" };
        _unreadable["poc_pull_token"] = "the master key changed";

        var create = () => Factory().Create(PocsConfigTests.SoundRegistry());

        create.Should().Throw<ContainerRegistryException>().WithMessage("*pull_auth*the master key changed*");
    }

    [Fact]
    public async Task ContainerRegistryFactory_PullAuth_EnvironmentFirst()
    {
        _secrets = [];
        _environment["poc_pull_token"] = FakeRegistryHost.PullToken;

        (await Factory().Create(PocsConfigTests.SoundRegistry()).ProbeAsync(CancellationToken.None)).Reached.Should().BeTrue();
    }

    [Fact]
    public void ContainerRegistryFactory_UnknownType_RefusedNamingKey()
    {
        var registry = PocsConfigTests.SoundRegistry();
        registry.Type = "ecr";

        var create = () => Factory().Create(registry, "token");

        create.Should().Throw<ContainerRegistryException>().WithMessage("pocs.targets[].registry.type is 'ecr'*");
    }

    private IContainerRegistry Registry() => Factory().Create(PocsConfigTests.SoundRegistry());

    private ContainerRegistryFactory Factory() => new(
        new CredentialResolver(new LoadedSecretValues(new AgentSmithConfig { Secrets = _secrets, UnreadableSecrets = _unreadable }),
            name => _environment.GetValueOrDefault(name)),
        new HostClients(_host), new CapturingLogger<ContainerRegistryFactory>());

    private sealed class HostClients(FakeRegistryHost host) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            name == ContainerRegistryFactory.ClientName ? host.Client() : throw new InvalidOperationException(name);
    }
}
