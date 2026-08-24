using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0504: the declaration gate. An unknown domain is refused where the value first
/// exists; a declared image is never overridden by a profile, because the image named
/// in the repository's own context.yaml is the image that gets used.
/// </summary>
public sealed class ContextDomainResolverTests
{
    private static readonly DomainProfile Profile = new(
        "sample-domain", "python:3.12-bookworm",
        ["buildpack-deps:bookworm-scm"],
        [new DomainProfileCommand("build", "make build")]);

    [Fact]
    public void Profile_UnknownDomain_RefusesBeforeAnySandbox_NamingValueContextSourceAndVersion()
    {
        var sut = TestDomainProfiles.Resolver(Profile);
        var discovery = new RemoteContextDiscovery("warehouse", ".", null, Domain: "not-a-domain");

        var act = () => sut.Resolve("data-repo", discovery);

        var message = act.Should().Throw<ConfigurationException>().Which.Message;
        message.Should().Contain("not-a-domain");           // the value read
        message.Should().Contain("data-repo");              // the repository
        message.Should().Contain("warehouse");              // the context
        message.Should().Contain("(test catalog)");         // resolved source + version
        message.Should().Contain("sample-domain");          // what the catalog does carry
    }

    [Fact]
    public void Profile_NoDomainDeclared_BehavesExactlyAsBefore()
    {
        var sut = TestDomainProfiles.Resolver(Profile);

        sut.Resolve("repo", new RemoteContextDiscovery("default", ".", "csharp")).Should().BeNull();
    }

    [Fact]
    public void Compatibility_DomainAndNoImage_TakesTheProfileImage()
    {
        var sut = TestDomainProfiles.Resolver(Profile);
        var discovery = new RemoteContextDiscovery("warehouse", ".", null, Domain: "sample-domain");

        sut.Resolve("repo", discovery)!.Image.Should().Be("python:3.12-bookworm");
    }

    [Fact]
    public void Compatibility_DomainAndACompatibleImage_KeepsTheContextImage()
    {
        var sut = TestDomainProfiles.Resolver(Profile);
        var discovery = new RemoteContextDiscovery(
            "warehouse", ".", null, ToolchainImage: "buildpack-deps:bookworm-scm",
            Domain: "sample-domain");

        // The profile resolves, but the chain never reaches its image: the declared one wins.
        sut.Resolve("repo", discovery).Should().NotBeNull();
        new SandboxImageChain()
            .Resolve(new AgentSmith.Contracts.Models.Configuration.ResolvedProject(), null,
                discovery.ToolchainImage, Profile.Image)
            .Should().Be("buildpack-deps:bookworm-scm");
    }

    /// <summary>
    /// The operator's standing rule overrides the spec's original refusal here: an
    /// explicit image that the profile does not know still WINS. What the mismatch buys
    /// is that it is reported from the file rather than discovered as "command not
    /// found" inside a running sandbox.
    /// </summary>
    [Fact]
    public void Compatibility_DomainAndAnUnknownImage_KeepsTheContextImageAndWarnsNamingBoth()
    {
        var logger = new CapturingLogger<ContextDomainResolver>();
        var sut = new ContextDomainResolver(new TestDomainProfiles(Profile), logger);
        var discovery = new RemoteContextDiscovery(
            "warehouse", ".", null, ToolchainImage: "node:20-bookworm", Domain: "sample-domain");

        sut.Resolve("repo", discovery).Should().NotBeNull();

        new SandboxImageChain()
            .Resolve(new AgentSmith.Contracts.Models.Configuration.ResolvedProject(), null,
                discovery.ToolchainImage, Profile.Image)
            .Should().Be("node:20-bookworm");
        var warning = logger.Warnings.Should().ContainSingle().Subject;
        warning.Should().Contain("node:20-bookworm").And.Contain("python:3.12-bookworm");
    }
}
