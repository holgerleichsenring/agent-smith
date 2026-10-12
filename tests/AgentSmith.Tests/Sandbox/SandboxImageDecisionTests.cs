using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-12-50bda: the chain's decision names the link that chose the image and carries a
/// refused stack.image beside it, instead of dropping it in a server log.
/// </summary>
public sealed class SandboxImageDecisionTests
{
    private const string Refused = "sbtscala/scala-sbt:eclipse-temurin-25_1.11.7_2.13.18";
    private readonly SandboxImageChain _sut = new();

    [Fact]
    public void Decide_RefusedContextImage_CarriesTheRefusalBesideTheImage()
    {
        var decision = _sut.Decide(new ResolvedProject(), "scala", $"  {Refused} ");

        decision.Should().Be(new SandboxImageDecision(
            SandboxImageChain.GenericFallbackImage, SandboxImageLink.GenericFallback, Refused));
        decision.SubstitutesTheDeclaredImage.Should().BeTrue();
        decision.FellThrough.Should().BeFalse("the context declared an image; it did not fall through");
    }

    [Fact]
    public void Decide_RefusedImageOfACatalogLanguage_KeepsTheCatalogImageAndTheRefusal() =>
        _sut.Decide(new ResolvedProject(), "go", "example.invalid/tools/go:1")
            .Should().Be(new SandboxImageDecision(
                "golang:1.22-bookworm", SandboxImageLink.Catalog, "example.invalid/tools/go:1"));

    [Fact]
    public void Decide_OperatorLink_NeverConsultsTheContextImage()
    {
        var project = new ResolvedProject
        {
            Sandbox = new SandboxConfig { Images = new Dictionary<string, string> { ["scala"] = "ghcr.io/team/sbt:1" } },
        };

        _sut.Decide(project, "scala", Refused)
            .Should().Be(new SandboxImageDecision("ghcr.io/team/sbt:1", SandboxImageLink.LanguageImage));
    }

    [Fact]
    public void Decide_NothingNamed_FellThrough()
    {
        var decision = _sut.Decide(new ResolvedProject(), "cobol", contextImage: null);

        decision.Link.Should().Be(SandboxImageLink.GenericFallback);
        decision.FellThrough.Should().BeTrue();
        decision.SubstitutesTheDeclaredImage.Should().BeFalse();
    }

    [Fact]
    public void Decide_TrustedContextImage_IsTheContextLink() =>
        _sut.Decide(new ResolvedProject(), "node", "node:22-bookworm")
            .Should().Be(new SandboxImageDecision("node:22-bookworm", SandboxImageLink.ContextImage));
}
