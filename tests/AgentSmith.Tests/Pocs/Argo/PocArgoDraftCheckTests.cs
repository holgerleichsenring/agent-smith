using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.DraftChecks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Argo;

/// <summary>2026-11-23-c81ec: the studio's Test of an argo block — values, the catalog token, its expiry, the probe and
/// the ApplicationSet's state.</summary>
public sealed class PocArgoDraftCheckTests
{
    private readonly FakeArgoCd _argo = new();

    [Fact]
    public async Task Run_ASoundDraft_WalksEveryStep_AndSaysTheApplicationSetIsMissing()
    {
        var report = await Check(FakeArgoHost.Jwt(null)).RunAsync(new PocArgoCheckRequest("main", PocsConfigTests.SoundArgo()), default);

        report.Steps.Select(s => s.Key).Should().Equal("secret", "token", "scope", "application_set");
        report.Steps[1].Detail.Should().Be("The token has no expiry.");
        report.Steps[3].Ok.Should().BeFalse();
        report.Steps[3].Detail.Should().Contain("pocs-main does not exist yet; run Install");
    }

    [Fact]
    public async Task Run_AnInstalledSet_Passes()
    {
        _argo.StoredSet = new JsonObject { ["spec"] = new JsonObject() };

        (await Check(FakeArgoHost.Jwt(null)).RunAsync(new PocArgoCheckRequest("main", PocsConfigTests.SoundArgo()), default)).Ok.Should().BeTrue();
    }

    [Fact]
    public async Task Run_ATokenThatIsNoJwt_StopsAtTheTokenStep()
    {
        var report = await Check("plain-token").RunAsync(new PocArgoCheckRequest("main", PocsConfigTests.SoundArgo()), default);

        report.Steps.Last().Should().Match<DraftCheckStep>(s => s.Key == "token" && !s.Ok);
    }

    [Fact]
    public async Task Run_ABadDraft_IsRefusedBeforeAnyRequest()
    {
        var argo = PocsConfigTests.SoundArgo();
        argo.Url = "http://argocd";

        var report = await Check(FakeArgoHost.Jwt(null)).RunAsync(new PocArgoCheckRequest("main", argo), default);

        report.Steps.Should().ContainSingle().Which.Key.Should().Be("values");
    }

    private PocArgoDraftCheck Check(string token) =>
        new(Mock.Of<ISecretValues>(s => s.Resolve("poc_argo_token") == token), _argo, new DraftCheckCollector(), TimeProvider.System,
            NullLogger<PocArgoDraftCheck>.Instance);
}
