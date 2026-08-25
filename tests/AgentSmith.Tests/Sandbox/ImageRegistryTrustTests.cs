using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-08-25-014d: which registries a sandbox image may come from is the
/// operator's to say — and an unset configuration says exactly what the compiled
/// list used to say, including the library-namespace SHAPE that is not a registry.
/// </summary>
public sealed class ImageRegistryTrustTests
{
    private static ImageRegistryTrust Trust(SandboxGlobalConfig? config = null) =>
        new(Options.Create(config ?? new SandboxGlobalConfig()));

    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/sdk:8.0", true)]
    [InlineData("ghcr.io/some-org/tool:1-bookworm", true)]
    [InlineData("node:20-bookworm", true)]
    [InlineData("buildpack-deps:bookworm-scm", true)]
    [InlineData("evil.example.com/pwn:latest", false)]
    [InlineData("someuser/node:20-bookworm", false)]
    public void Registries_Unset_AcceptsExactlyWhatIsAcceptedToday(string image, bool trusted) =>
        Trust().Accepts(image).Should().Be(trusted);

    [Fact]
    public void Registries_Unset_StillAcceptsTheLibraryNamespaceShape()
    {
        // The shape rule, not a registry rule: no namespace segment at all. It survives
        // an unset configuration, and it is what a registry entry could not have said.
        var trust = Trust();

        trust.Accepts("golang:1.22-bookworm").Should().BeTrue();
        trust.Accepts("someuser/golang:1.22-bookworm").Should().BeFalse(
            "a namespaced repository on the same host is a different author");
    }

    [Fact]
    public void Registries_Unset_ReadsAPortAsAPortAndNotAsATag()
    {
        // `a.host:5000/pwn:latest` cut at the FIRST colon leaves "a.host" — no slash,
        // so the shape rule would have called an arbitrary private registry an official
        // library image. The boundary this phase moved into configuration is the same
        // boundary it had to state correctly.
        Trust().Accepts("a.host:5000/pwn:latest").Should().BeFalse();
    }

    [Fact]
    public void Registries_Configured_RefusesAnImageOutsideThem()
    {
        var trust = Trust(new SandboxGlobalConfig
        {
            AllowedRegistries = ["registry.example.com"],
        });

        trust.Accepts("registry.example.com/team/toolchain:1").Should().BeTrue(
            "an entry without a trailing slash is still a registry prefix");
        trust.Accepts("mcr.microsoft.com/dotnet/sdk:8.0").Should().BeFalse(
            "a named list replaces the built-in default rather than extending it");
        trust.Accepts("node:20-bookworm").Should().BeFalse(
            "a named list is a narrowing; keeping the library shape would widen it back open");
    }

    [Fact]
    public void Registries_Configured_KeepTheLibraryShapeOnlyWhenTheOperatorSaysSo()
    {
        var trust = Trust(new SandboxGlobalConfig
        {
            AllowedRegistries = ["registry.example.com"],
            AllowDockerHubLibrary = true,
        });

        trust.Accepts("node:20-bookworm").Should().BeTrue();
        trust.Accepts("someuser/node:20-bookworm").Should().BeFalse();
    }

    [Fact]
    public void Registries_Unset_TheLibraryShapeCanBeSwitchedOffOnItsOwn()
    {
        var trust = Trust(new SandboxGlobalConfig { AllowDockerHubLibrary = false });

        trust.Accepts("node:20-bookworm").Should().BeFalse();
        trust.Accepts("mcr.microsoft.com/dotnet/sdk:8.0").Should().BeTrue(
            "the registries are untouched — the shape is its own switch");
    }

    [Fact]
    public void Registries_PerProject_CannotWidenTheGlobalBoundary()
    {
        // The supply-chain boundary has exactly one home. A per-project `sandbox:` block
        // that could name registries would let a project widen its own, which is not a
        // boundary — so the per-project shape must offer no way to express it.
        typeof(SandboxConfig).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(n =>
                n.Contains("Registries", StringComparison.Ordinal)
                || n.Contains("Library", StringComparison.Ordinal));

        typeof(SandboxGlobalConfig).GetProperty(nameof(SandboxGlobalConfig.AllowedRegistries))
            .Should().NotBeNull("the policy is global, and only global");
    }

    [Fact]
    public void Description_NamesTheKeysAnOperatorWouldWiden()
    {
        Trust().Description.Should()
            .Contain("sandbox.allowed_registries")
            .And.Contain("mcr.microsoft.com/");
    }
}
