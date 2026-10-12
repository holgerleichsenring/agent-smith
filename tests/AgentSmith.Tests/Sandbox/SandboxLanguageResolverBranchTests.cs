using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-12-6bfaa: the live shape — a Scala project whose context.yaml exists only on the ticket
/// branch (its init pull request is not merged). Read on the default branch, the run resolved no
/// language and its sandbox fell to the generic image; read on the ticket branch it resolves scala.
/// </summary>
public sealed class SandboxLanguageResolverBranchTests
{
    private const string ContextYaml = ".agentsmith/contexts/default/context.yaml";
    private static readonly BranchName Ticket = new("agent-smith/4711");
    private readonly Mock<ISourceProvider> _provider = new();
    private readonly RepoConnection _source = new() { Name = "app", Url = "https://example.com/app.git" };

    [Fact]
    public async Task ContextOnTicketBranchOnly_ResolvesItsLanguage()
    {
        _provider.Setup(p => p.BranchExistsAsync(Ticket, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _provider.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", Ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["default"]);
        _provider.Setup(p => p.TryReadFileAsync(ContextYaml, Ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync("meta:\n  workdir: .\n  project: app\nstack:\n  lang: scala\n");

        var result = await Resolver().ResolveAllAsync(_source, ContextReadBranches.ForBranch(Ticket), CancellationToken.None);

        var discovery = result.Should().ContainSingle().Subject;
        discovery.Language.Should().Be("scala");
        discovery.Provenance.Should().Be($"{ContextYaml} read on branch 'agent-smith/4711'");
        _provider.Verify(p => p.TryReadFileAsync(ContextYaml, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoContextsAnywhere_SyntheticNamesWhatWasSearched()
    {
        _provider.Setup(p => p.BranchExistsAsync(Ticket, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _provider.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await Resolver().ResolveAllAsync(_source, ContextReadBranches.ForBranch(Ticket), CancellationToken.None);

        var synthetic = result.Should().ContainSingle().Subject;
        synthetic.Language.Should().BeNull();
        synthetic.Provenance.Should().Contain("no context.yaml under .agentsmith/contexts/")
            .And.Contain("the default branch")
            .And.Contain("branch 'agent-smith/4711' is not on the remote");
    }

    [Fact]
    public async Task ContextOverride_ReadsTheNamedContextOnTheLocatedBranch()
    {
        _provider.Setup(p => p.BranchExistsAsync(Ticket, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _provider.Setup(p => p.ListDirectoryAsync(".agentsmith/contexts", Ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["default"]);
        _provider.Setup(p => p.TryReadFileAsync(ContextYaml, Ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync("meta:\n  workdir: .\n  project: app\nstack:\n  lang: scala\n");

        var result = await Resolver().ResolveContextAsync(
            _source, "default", ContextReadBranches.ForBranch(Ticket), CancellationToken.None);

        result.Should().ContainSingle().Which.Language.Should().Be("scala");
    }

    private Application.Services.Sandbox.SandboxLanguageResolver Resolver()
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        return TestSandboxLanguageResolver.Create(
            factory.Object, new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())));
    }
}
