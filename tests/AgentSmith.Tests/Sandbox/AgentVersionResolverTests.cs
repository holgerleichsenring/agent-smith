using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-25-0d01: two independently declarable versions that must match ARE the
/// configuration bug — the field was mandatory, its own error text told the operator to
/// pick a tag "matching the agent-smith release in use", and nothing enforced that. So the
/// version is derived, and the override survives as the only way to get a difference.
/// </summary>
public sealed class AgentVersionResolverTests
{
    [Fact]
    public void AgentVersion_NotDeclared_IsDerivedFromTheServer()
    {
        var choice = Sut(declared: "", serverVersion: "0.135.0").Resolve(new ResolvedProject());

        choice.Version.Should().Be("0.135.0");
        choice.IsPinned.Should().BeFalse();
        choice.DiffersFromServer.Should().BeFalse("there is nothing left to forget to move");
    }

    [Fact]
    public void AgentVersion_DeliberatelyPinned_IsUsed()
    {
        var choice = Sut(declared: "0.121.0", serverVersion: "0.135.0").Resolve(new ResolvedProject());

        choice.Version.Should().Be("0.121.0",
            "an air-gapped mirror and a bisecting developer both need the override");
        choice.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void AgentVersion_PinnedPerProject_WinsOverTheGlobalPin()
    {
        var project = new ResolvedProject { Sandbox = new SandboxConfig { AgentVersion = "0.74.1" } };

        Sut(declared: "0.121.0", serverVersion: "0.135.0").Resolve(project)
            .Version.Should().Be("0.74.1");
    }

    [Fact]
    public void AgentVersion_PinnedToTheServersOwnRelease_IsNotADifference()
    {
        Sut(declared: "0.135.0", serverVersion: "0.135.0").Resolve(new ResolvedProject())
            .DiffersFromServer.Should().BeFalse();
    }

    [Fact]
    public void AgentVersion_PinnedOnABuildThatCannotNameItsRelease_IsNotADifference()
    {
        Sut(declared: "0.121.0", serverVersion: null).Resolve(new ResolvedProject())
            .DiffersFromServer.Should().BeFalse("a half that does not know what it is says "
                + "nothing, and silence is not a mismatch");
    }

    [Fact]
    public void AgentVersion_NeitherDeclaredNorDerivable_SaysHowToNameOne()
    {
        var act = () => Sut(declared: "", serverVersion: null).Resolve(new ResolvedProject());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*sandbox.agent_version*");
    }

    private static AgentVersionResolver Sut(string declared, string? serverVersion)
    {
        var global = Options.Create(new SandboxGlobalConfig { AgentVersion = declared });
        return new AgentVersionResolver(global, new BuildIdentity("deadbeefcafe", serverVersion));
    }
}
